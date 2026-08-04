import hydra
from attrs import define
from crew_algorithms.envs.configs import EnvironmentConfig, register_env_configs
from crew_algorithms.wildfire_alg.config.configs import LLMConfig
from crew_algorithms.utils.wandb_utils import WandbConfig
from crew_algorithms.wildfire_alg.config.build_config import update_config, create_level_presets
from hydra.core.config_store import ConfigStore
from omegaconf import MISSING
import numpy as np
from crew_algorithms.wildfire_alg.core.alg_utils import get_agent_observations, generate_action_from_option, parse_game_data, check_if_option_done, check_game_done
import datetime
import csv
import certifi
from crew_algorithms.wildfire_alg.data.render_logs import compile_split_screen_video
from crew_algorithms.wildfire_alg.core.paths import build_results_path



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
    from crew_algorithms.wildfire_alg.algorithms.COELA.agent import Agent
    from crew_algorithms.wildfire_alg.algorithms.COELA.utils import translate_action, generate_communication, generate_action, Action
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

    cfg.envs.algorithm = 'COELA'
   
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
    path = build_results_path("COELA", level, seed, cfg.envs.timestamp)
    os.makedirs(path, exist_ok=True)

    
    
    agents = []
    game_data = parse_game_data(state, cfg)
    print(f"Task: {game_data['task_description']}")

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

    global_data = {}
    global_data.update({
        "api_calls" : 0,
        "input_tokens": 0,
        "output_tokens": 0,
        "score": 0
    })
    print("Agent Count: " + str(len(agents)))

    header = ["cumulative_score", "cumulative_api_calls","cumulative_input_tokens", "cumulative_output_tokens"]
    csv_filename = os.path.join(path, f"data.csv")
    with open(csv_filename, 'w', newline='') as f:
          writer = csv.writer(f)
          writer.writerow(header)
    f.close()


    for t in range(cfg.envs.max_steps):
        print(f"TIME: {t}")
        game_data = parse_game_data(state, cfg)

        removelist = []
        past_score = global_data["score"]
        global_data.update({
            'firefighters': [],
            'bulldozers': [],
            'drones': [],
            'helicopters': [],
            'time': t,
            'agents': agents,
            'score': game_data["score"],
        })




        with open(csv_filename, 'a', newline='') as f:
            writer = csv.writer(f)
            writer.writerow([global_data['score'], global_data['api_calls'], global_data['input_tokens'], global_data['output_tokens']])
        f.close()
        agent_states = {}

        for agent in agents:
            #If last action was marked as 'done', remove option and set as idle
            
            observations = get_agent_observations(state, agent.id)
            if observations["agent_type"] >=4:
                print(f"Agent {agent.id} DESTROYED")
                removelist.append(agent)
                continue
            elif observations["agent_type"] ==0:
                global_data["firefighters"].append(agent)
            elif observations["agent_type"] ==1:
                global_data["bulldozers"].append(agent)
            elif observations["agent_type"] ==2:
                global_data["drones"].append(agent)
            elif observations["agent_type"] ==3:
                global_data["helicopters"].append(agent)
            
            agent.last_observation = observations["perception_grid"]
            agent.last_position = observations["position"]
            agent.last_current_cell = observations["current_cell"]
            agent.map_range = observations["map_range"]
            agent.extra_variables = observations["extra_variables"]
            check_if_option_done(agent=agent)
            

            if agent.type==0 and agent.extra_variables[2]==1:
                agent.options = [Action(type=0, param_1=0, param_2=0,description="ride helicopter")]

            agent_states.update({agent.id: agent.last_position})

        for r in removelist:
            agents.remove(r)

        if check_game_done(global_data=global_data, cfg= cfg.envs, past_score=past_score):
            break

        for agent in agents:
            print("generating perception")
            agent.generate_perception(cfg.envs, agent_states, global_data)

        


        env_action = [[0,0,0] for _ in range(cfg.envs.num_agents)]
        for agent in agents:
            if len(agent.options)>0:
                pass
            else:
                proposed_message = generate_communication(agent, global_data)
                action = generate_action(agent, global_data, proposed_message)

                if "SEND_MESSAGE" in action:
                    action = action.replace("SEND_MESSAGE", "")
                    for a in agents:
                        a.add_message(f"AGENT {agent.id}", action, t)
                    agent.options = [Action(type = 0, param_1=0, param_2=0, description="send message")]

                else:
                    option = translate_action(action, type = agent.type, global_data=global_data)
                    agent.options = [option]
                    

                
            agent.log_chat("Executing Actions", [("system", agent.options[0])])
            print(f"AGENT_{agent.id}: {agent.options[0].description}")
            action_array = generate_action_from_option(agent=agent)

            env_action[agent.id] = action_array
            agent.log_chat("", [("system", action_array)])
        print(env_action)
            

        action_tensor = torch.from_numpy(np.array(env_action)).to(device)
        state["agents"]["action"] = action_tensor
        newstate = env.step(state)
        state["agents"]["observation"] = newstate["next"]["agents"]["observation"]


    env.close()
    print("TEST COMPLETE")
    compile_split_screen_video(path, os.path.join(path, "render.mp4"))
            

if __name__ == "__main__":

    wildfire_alg()
