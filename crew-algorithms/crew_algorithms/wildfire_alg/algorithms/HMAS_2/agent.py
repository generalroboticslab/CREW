from openai import OpenAI
from crew_algorithms.wildfire_alg.algorithms.HMAS_2.__main__ import Config
from crew_algorithms.wildfire_alg.core.paths import build_agent_chat_path
from typing import List, Tuple, Dict

class Agent:
    def __init__(
        self,
        id: int,
        type: int,
        cfg: Config,
        path: str,
        current_task: str,
        api_key: str,
        agent_count: int
    ) -> None:
        self.id = id
        self.type = type
        self.cfg = cfg
        self.path = path
        self.current_task = current_task
        self.api_key = api_key
        # Chat history for each other agent and a GLOBAL channel
        self.chat_history: Dict[str, List[Tuple[str, str, int]]] = {
            f"AGENT_{i+1}": [] for i in range(agent_count) if (i+1) != id
        }
        self.chat_history["GLOBAL"] = []

        self.past_actions: List[str] = []
        self.last_observation: str = None
        self.last_position: Tuple[int, int] = None
        self.last_current_cell: str = None
        self.last_perceptions: str = None
        self.map_range: int = 0
        self.extra_variables = []


    def log_chat(self, chat_name: str, messages: List[Tuple[str, str]]):
        chat_string = f"{chat_name}\n" + "-"*20 + "\n\n"
        for source, content in messages:
            chat_string += f"{source}\n-----\n{content}\n-----\n\n"
        chat_string += "-"*20 + "\nEND CHAT\n\n"
        filepath = build_agent_chat_path(self.path, self.id)
        with open(filepath, "a", encoding="utf-8") as file:
            file.write(chat_string)
        file.close()

                    


    def generate_perception(self, cfg:Config, agent_states: Dict[int, Tuple[int, int]], global_data:dict):
        print(f"agent {self.id}: generating perception")
        # Build observation string verbatim

        # in helicopter
        if self.type == 0 and self.extra_variables[2]==1:
            obs_string = f"""
            You are AGENT_{self.id} and you are within a helicopter. You are unable to perform actions. Your current location is POSITION.

            """
        else:
            obs_string = f"""
                You are AGENT_{self.id}, and your current location is POSITION and thus your minimap view will be the range MAPRANGE with the top corner of the map being (0,0).
                
                This is your minimap view: 
                \n OBS \n 
                Each cell is represented by a character corresponding to the type of terrain:
                    0: brush (no trees)
                    1: light forest (1 tree)
                    2: medium forest (2 trees)
                    3: dense forest (3 trees)
                    i: Ignited
                    f: On Fire
                    e: Extinguishing
                    x: Fully Extinguished
                    w: Water Source Cell (no trees)
                    B: building (no trees)
                    
                IGNORE ALL "-". Those are unrevealed cells. They will reveal themselves when you get closer to them.

                The cells in single quotations are wet cells. 'C' cells are civilians.
                
                The bolded cell is the current cell you are in. It is a CURRCELL cell at POSITION. There are other nearby agents at: 

                OTHERS

                EXTRA

                """
        extra_string = ""
        if self.type == 0:
            if self.extra_variables[0]==0:
                extra_string+="You are not carrying any civilians.\n"
            else:
                extra_string+="You are carrying a civilian.\n"
                
            extra_string+=f"You current have {int(self.extra_variables[1])} water to spray"
        if self.type == 3:
            if self.extra_variables[0]==0:
                extra_string+="You are not carrying any firefighters.\n"
            else:
                extra_string+=f"You are carrying {int(self.extra_variables[0])} firefighters.\n"
                
            extra_string+=f"You current have {int(self.extra_variables[1])}/5 water to deploy"


        maprange_string = (
            f"x: [{self.last_position[0]-self.map_range//2}, {self.last_position[0]+self.map_range//2}]\n"
            f"y: [{self.last_position[1]-self.map_range//2}, {self.last_position[1]+self.map_range//2}]"
        )
        obs_string = obs_string.replace("POSITION", str(self.last_position))
        obs_string = obs_string.replace("MAPRANGE", maprange_string)
        obs_string = obs_string.replace("OBS", self.last_observation)
        obs_string = obs_string.replace("CURRCELL", self.last_current_cell)
        obs_string = obs_string.replace("EXTRA",extra_string)
        others = ""
        for a, pos in agent_states.items():
            if a != self.id and abs(pos[0]-self.last_position[0]) < self.map_range and abs(pos[1]-self.last_position[1]) < self.map_range:
                others += f"AGENT_{a}: {pos} "
        obs_string = obs_string.replace("OTHERS", others)
        if self.type == 0:
            type_string = 'Firefighter'
        elif self.type == 1:
            type_string = 'Bulldozer'
        elif self.type == 2:
            type_string = 'Drone'
        else:
            type_string = 'Helicopter'
        system_message = f"""
                You are the Perception Module of an embodied {type_string} agent, AGENT_{self.id}, within a large grid world spanning from x:[0 to {cfg.map_size-1}] and y:[0 to {cfg.map_size-1}]. 
                Your job is to process and understand your surroundings. 
                Do not directly report explicit information from the minimap, but rather spatially understand your surroundings.
                Do not refer to character representations of the minimap, only what they actually represent.
                Report general observations in general directions.
                Also report if there are specific cells of interest, such as fires, civilians, water, etc. 
                If there are any, calculate their exact locations by explicitly counting cells. 

                You should return a detailed but concise text summary paragraph of all relevant information, including location, surroundings, and presence of important cells.
                """
        user_message = (
            f"Here are your observations: \n\n{obs_string}\n\n"
            f"Create a detailed text summary of all relevant information, such as location, surroundings, presence of fire and civilians, etc. Speak only in first person as AGENT_{self.id}. "
        )

        client = OpenAI(api_key=self.api_key)
        response = client.chat.completions.create(
            model="gpt-4o",
            messages=[
                {"role": "system", "content": system_message},
                {"role": "user", "content": user_message}
            ],
            temperature=0
        )
        global_data["api_calls"]+=1
        global_data["input_tokens"]+=response.usage.prompt_tokens
        global_data["output_tokens"]+=response.usage.completion_tokens


        perception = response.choices[0].message.content
        self.log_chat("Summarizing Observations", [("user", user_message), ("assistant", perception)])
        self.last_perception = perception
        client.close()
        return perception
