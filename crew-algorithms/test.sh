python crew_algorithms/ddpg envs=bowling collector.frames_per_batch=120 batch_size=120 hf=False feedback_model=False seed=42 envs.time_scale=1 sub_name=release_test train_batches=5
python crew_algorithms/ddpg envs=find_treasure collector.frames_per_batch=240 batch_size=240 hf=False feedback_model=False seed=42 envs.time_scale=1 sub_name=release_test train_batches=10
python crew_algorithms/ddpg envs=hide_and_seek_1v1 collector.frames_per_batch=240 batch_size=240 hf=False feedback_model=False seed=42 envs.time_scale=1 sub_name=release_test train_batches=10

python crew_algorithms/ddpg envs=find_treasure collector.frames_per_batch=240 batch_size=240 hf=False feedback_model=False heuristic_feedback=True seed=42 envs.time_scale=1 sub_name=release_test train_batches=10
python crew_algorithms/ddpg envs=hide_and_seek_1v1 collector.frames_per_batch=240 batch_size=240 hf=False feedback_model=False heuristic_feedback=True seed=42 envs.time_scale=1 sub_name=release_test train_batches=10

python crew_algorithms/sac envs=bowling collector.frames_per_batch=120 batch_size=120 hf=False feedback_model=False seed=42 envs.time_scale=1 sub_name=release_test train_batches=5
python crew_algorithms/sac envs=find_treasure collector.frames_per_batch=240 batch_size=240 hf=False feedback_model=False seed=42 envs.time_scale=1 sub_name=release_test train_batches=10
python crew_algorithms/sac envs=hide_and_seek_1v1 collector.frames_per_batch=240 batch_size=240 hf=False feedback_model=False seed=42 envs.time_scale=1 sub_name=release_test train_batches=10

python crew_algorithms/deep_tamer envs=bowling seed=42 envs.time_scale=1 sub_name=release_test train_minutes=5
python crew_algorithms/deep_tamer envs=find_treasure seed=42 envs.time_scale=1 sub_name=release_test train_minutes=10
python crew_algorithms/deep_tamer envs=hide_and_seek_1v1 seed=42 envs.time_scale=1 sub_name=release_test train_minutes=10