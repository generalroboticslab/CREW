import hydra
from attrs import define
from crew_algorithms.envs.configs import EnvironmentConfig, register_env_configs
from crew_algorithms.wildfire_alg.config.configs import LLMConfig
from crew_algorithms.utils.wandb_utils import WandbConfig
from crew_algorithms.wildfire_alg.config.build_config import update_config, create_level_presets
from hydra.core.config_store import ConfigStore
from omegaconf import MISSING
import numpy as np
from crew_algorithms.wildfire_alg.core.alg_utils import get_agent_observations, parse_game_data, check_game_done
import datetime
import csv
import certifi
import os
from crew_algorithms.wildfire_alg.data.render_logs import compile_split_screen_video
from crew_algorithms.wildfire_alg.core.paths import build_results_path

from crew_algorithms.wildfire_alg.libraries.firefighter_action_library import Run_Firefighter_Action
from crew_algorithms.wildfire_alg.libraries.bulldozer_action_library import Run_Bulldozer_Action
from crew_algorithms.wildfire_alg.libraries.drone_action_library import Run_Drone_Action
from crew_algorithms.wildfire_alg.libraries.helicopter_action_library import Run_Helicopter_Action




@define(auto_attribs=True)
class Config:
    envs: EnvironmentConfig = MISSING
    """Settings for the environment to use."""
    wandb: WandbConfig = WandbConfig(project="wildfire")
    """WandB logger configuration."""
    collect_data: bool = False
    """Whether or not to collect data and save a new dataset to WandB."""
    llms: LLMConfig = LLMConfig()


cs = ConfigStore.instance()
cs.store(name="base_config", node=Config)


register_env_configs()


@hydra.main(version_base=None, config_path="../../../conf", config_name="wildfire_alg")


def wildfire_alg(cfg: Config):

    """An implementation of a wildfire alg."""
    import os
    import uuid
    from pathlib import Path


    import torch
    from crew_algorithms.envs.channels import ToggleTimestepChannel
    from crew_algorithms.wildfire_alg.core.utils import (
        make_env,
    )
    from crew_algorithms.wildfire_alg.algorithms.MANUAL.agent import Agent

    from torchrl.record.loggers import generate_exp_name, get_logger

    # wandb.login()
    # exp_name = generate_exp_name("Wildfire", f"random-{cfg.envs.name}")
    # logger = get_logger(
    #     "wandb", 
    #     logger_name=os.getcwd(),
    #     experiment_name=exp_name,
    #     wandb_kwargs=dict(
    #         entity=cfg.wandb.entity,
    #         project=cfg.wandb.project,
    #         settings=wandb.Settings(start_method="thread"),
    #         tags=["baseline", cfg.envs.name],
    #     ),
    # )
    #logger.log_hparams(cfg)
    device = "cpu" if not torch.has_cuda else "cuda:0"
    toggle_timestep_channel = ToggleTimestepChannel(uuid.uuid4())

    # Sets up environment/game configs



    cfg.envs.algorithm = 'MANUAL'
    
    level = cfg.envs.level
    seed  = cfg.envs.seed

    levels = create_level_presets()


    firefighters = levels[level].get("starting_firefighter_agents",0)
    bulldozers = levels[level].get("starting_bulldozer_agents",0)
    drones = levels[level].get("starting_drone_agents",0)
    helicopters = levels[level].get("starting_helicopter_agents",0)
    agent_count = firefighters + bulldozers + drones + helicopters


    update_config(preset=levels[level], config=cfg.envs, log_trajectory=True, seed=seed)

    cfg.envs.timestamp = datetime.datetime.now().strftime("%Y-%m-%d-%H-%M-%S")  
    env = make_env(cfg.envs, toggle_timestep_channel, device)
    state = env.reset()

    os.environ["OPENAI_API_KEY"] = os.getenv("OPENAI_API_KEY")
    os.environ["SSL_CERT_FILE"] = certifi.where()
    api_key = os.environ['OPENAI_API_KEY']
    
    path = build_results_path("MANUAL", level, seed, cfg.envs.timestamp)
    os.makedirs(path, exist_ok=True)

    
    
    agents = []
    game_data = parse_game_data(state, cfg)

    
    for i in range(firefighters):
        a = Agent(i+1, 0, cfg, path, current_task=game_data["task_description"], api_key=api_key)
        agents.append(a)
    for i in range(bulldozers):
        a = Agent(firefighters+i+1, 1, cfg, path, current_task=game_data["task_description"], api_key=api_key)
        agents.append(a)
    for i in range(drones):
        a = Agent(firefighters+bulldozers+i+1, 2, cfg, path, current_task=game_data["task_description"], api_key=api_key)
        agents.append(a)
    for i in range(helicopters):
        a = Agent(firefighters+bulldozers+drones+i+1, 3, cfg, path, current_task=game_data["task_description"], api_key=api_key)
        agents.append(a)

    header = ["cumulative_score", "cumulative_api_calls","cumulative_input_tokens", "cumulative_output_tokens"]
    csv_filename = os.path.join(path, f"data.csv")
    with open(csv_filename, 'w', newline='') as f:
          writer = csv.writer(f)
          writer.writerow(header)
    f.close()
    
    for t in range(cfg.envs.max_steps):
        print(f"TIME: {t}")
        print(f"Task: {game_data['task_description']}")

        with open(csv_filename, 'a', newline='') as f:
            writer = csv.writer(f)
            writer.writerow([game_data['score'], 0, 0, 0])
        f.close()
        game_data = parse_game_data(state, cfg)

        removelist = []
        for agent in agents:
            observations = get_agent_observations(state, agent.id)
            if observations["agent_type"] >=4:
                print(f"Agent {agent.id} DESTROYED")
                removelist.append(agent)
                continue

            
            agent.last_observation = observations["perception_grid"]
            agent.last_position = observations["position"]
            agent.last_current_cell = observations["current_cell"]
            agent.map_range = observations["map_range"]
            agent.extra_variables = observations["extra_variables"]

        for r in removelist:
            agents.remove(r)
        
        env_action = [[0,0,0] for _ in range(cfg.envs.num_agents)]

        #TODO: Implement the MANUAL algorithm
        for agent in agents:
            print(f"\n{'='*50}")
            print(f"Agent {agent.id} (Type: {['Firefighter', 'Bulldozer', 'Drone', 'Helicopter'][agent.type]})")
            print(f"Position: {agent.last_position}")
            print(f"Current Cell: {agent.last_current_cell}")
            print(f"Map Range: {agent.map_range}")
            print(f"Perception Grid:\n{agent.last_observation}")
            
            # Display extra information based on agent type
            if agent.type == 0:  # Firefighter
                print("\nStatus:")
                if agent.extra_variables[0] == 0:
                    print("- Not carrying any civilians")
                else:
                    print("- Currently carrying a civilian")
                print(f"- Water available: {int(agent.extra_variables[1])} units")
                
                print("\nAvailable actions:")
                print("1. Move to location")
                print("2. Cut tree")
                print("3. Pick up civilian")
                print("4. Drop off civilian")
                print("5. Spray water")
                print("6. Refill water")    
                print("0. Do nothing")
                
            elif agent.type == 1:  # Bulldozer
                print("\nAvailable actions:")
                print("1. Move to location")
                print("2. Move while cutting trees")
                print("0. Do nothing")
                
            elif agent.type == 2:  # Drone
                print("\nAvailable actions:")
                print("1. Move to location")
                print("0. Do nothing")
                
            elif agent.type == 3:  # Helicopter
                print("\nStatus:")
                if agent.extra_variables[0] == 0:
                    print("- Not carrying any firefighters")
                else:
                    print(f"- Currently carrying {int(agent.extra_variables[0])} firefighters")
                print(f"- Water available: {int(agent.extra_variables[1])}/5 units")
                
                print("\nAvailable actions:")
                print("1. Move to location")
                print("2. Pick up firefighters")
                print("3. Drop off firefighters")
                print("4. Refill water")
                print("5. Deploy water")
                print("0. Do nothing")

            # Get action type first
            action_type = int(input(f"\nEnter action type for Agent {agent.id}: "))
            param_1 = 0
            param_2 = 0
            
            # Get additional parameters based on action type and agent type
            if action_type == 1:  # Move actions for all agents
                param_1 = int(input("Enter target X coordinate: "))
                param_2 = int(input("Enter target Y coordinate: "))
            elif agent.type == 0 and action_type == 5:  # Spray water for firefighter
                if agent.extra_variables[1] <= 0:
                    print("Warning: No water available to spray!")
                param_1 = int(input("Enter target X coordinate for water spray: "))
                param_2 = int(input("Enter target Y coordinate for water spray: "))
            elif agent.type == 1 and action_type == 2:  # Move while cutting for bulldozer
                param_1 = int(input("Enter target X coordinate: "))
                param_2 = int(input("Enter target Y coordinate: "))
            elif agent.type == 3 and action_type == 5:  # Deploy water for helicopter
                if agent.extra_variables[1] <= 0:
                    print("Warning: No water available to deploy!")

            # Create action object
            class Action:
                def __init__(self, type, param_1, param_2, description=""):
                    self.type = type
                    self.param_1 = param_1
                    self.param_2 = param_2
                    self.description = description

            action = Action(action_type, param_1, param_2)

            # Translate action based on agent type
            if agent.type == 0:  # Firefighter
                env_action[agent.id-1] = Run_Firefighter_Action(agent, action)
            elif agent.type == 1:  # Bulldozer
                env_action[agent.id-1] = Run_Bulldozer_Action(agent, action)
            elif agent.type == 2:  # Drone
                env_action[agent.id-1] = Run_Drone_Action(agent, action)
            elif agent.type == 3:  # Helicopter
                env_action[agent.id-1] = Run_Helicopter_Action(agent, action)

        print(f"\nExecuting actions: {env_action}")

        action_tensor = torch.from_numpy(np.array(env_action)).to(device)
        state["agents"]["action"] = action_tensor
        newstate = env.step(state)
        state["agents"]["observation"] = newstate["next"]["agents"]["observation"]


    env.close()
    print("TEST COMPLETE")
    compile_split_screen_video(path, os.path.join(path, "render.mp4"))

if __name__ == "__main__":

    wildfire_alg()
