.. currentmodule:: crew_algorithms.deep_tamer

DeepTamer
=========

.. automodule:: crew_algorithms.deep_tamer

Algorithm
---------
The pivotal assumption behind DeepTamer is that human feeback :math:`H` at timestep :math:`t` approximates the discounted return :math:`R_{t}=\sum_{n=t}^{\infty}\gamma^{n-t}r_{n}` that
value functions usually approximate in standard reinforcement learning setting. Bellmen equations, which propagate local rewards through the state space, become unnecessary under this
assumption, since human feedbacks have already summarized the future rewards into a single value :math:`H`.

The main objective of this algorithm is to predict the feedback a human would give.

A natural idea following this assumption, then would be to apply the human feedback to direct policy gradients. Namely, to replace :math:`R_{t}` in :math:`\mathbb{E}_{\pi}[R_{t}\nabla_{\theta}\log\pi_{\theta}(a|s)]` with :math:`H`, so that we get the central update rule :math:`\theta\leftarrow\theta+\mathbb{E}_{\pi}[f_{t}\nabla_{\theta}\log\pi_{\theta}(a|s)]`.

There exist more considerations beyond the update rule to make DeepCoach more practical in real applications. Among them are **Importance Weights** for prioritizing certain samples when computing a loss.

With **Importance Weights**, we assign the importance of the loss of a sample based on the difference in time between when the sample occurred and the time the feedback was given. A distribution
can be used to determine how to bias the importance of these samples. The original paper uses a uniform distribution over a small period, but other distributions can be used.

For a complete algorithm discription, please refer to the following pseudocode:

.. image:: /images/deep_tamer_algorithm.*

In summary, DeepTamer is a policy-based reinforcement learning algorithm. It utilizes policy gradients to update its policy, while applying human feedbacks to replace discounted cumulative rewards that a critic usually approximates in Actor-Critc paradigm.

Running
-------

To run the DeepCoach algorithm, run the following command from the shell::

    python crew_algorithms/deep_tamer <OTHER CONFIGURATION OPTIONS HERE>

Training Results
----------------

You can view the training of the DeepTamer algorithm on both the Bowling and Tetris environments.

Bowling
^^^^^^^

.. video:: https://storage.googleapis.com/wandb-production.appspot.com/grl-crew/deep-tamer/1ya1wf5k/DeepTamer.mp4
   :width: 750

Tetris
^^^^^^

.. video:: https://storage.googleapis.com/wandb-production.appspot.com/grl-crew/deep-tamer/0cjv7oin/DeepTamer.mp4
   :width: 750
