# Algorithms
Algorithms for CREW

## Setup

1) Install miniconda
2) bash install.sh


If you want to contribute to this repository, you can install the pre-commit hooks for some nice features:
```bash
poetry run pre-commit install
```

export PATH="$/Users/michael/.local/bin.poetry/bin:$PATH"

## GUIDE
The GUIDE[1] algorithm is implemented within this directory. To run GUIDE with a ddpg backbone:

```python
python crew_algorithms/ddpg envs=bowling collector.frames_per_batch=120 batch_size=120 train_batches=30 hf=True
```

[1] Zhang, Lingyu, Zhengran Ji, Nicholas R Waytowich and Boyuan Chen. "GUIDE: Real-Time Human-Shaped Agents." Thirty-eighth Conference on Neural Information Processing Systems.
