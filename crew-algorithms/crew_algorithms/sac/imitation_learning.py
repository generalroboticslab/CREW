import torch
from tensordict.nn import TensorDictModule, TensorDictModuleWrapper
from tensordict.tensordict import TensorDictBase
from tensordict.utils import NestedKey


class ImitationLearningWrapper(TensorDictModuleWrapper):
    def __init__(
        self,
        policy: TensorDictModule,
        *,
        action_key: NestedKey | None = "action",
        il_enabled_key: NestedKey | None = "il_enabled",
        il_action_key: NestedKey | None = "il_action",
    ):
        super().__init__(policy)
        self.action_key = action_key
        self.il_enabled_key = il_enabled_key
        self.il_action_key = il_action_key

    def forward(self, tensordict: TensorDictBase) -> TensorDictBase:
        tensordict = self.td_module.forward(tensordict)
        if len(tensordict.batch_size) != 0:
            return tensordict
        
        if isinstance(self.action_key, tuple) and len(self.action_key) > 1:
            action_tensordict = tensordict.get(self.action_key[:-1])
            action_key = self.action_key[-1]
        else:
            action_tensordict = tensordict
            action_key = self.action_key

        predicted_action = action_tensordict.get(action_key)
        act_dims = predicted_action.shape[-1]

        tensordict[self.il_enabled_key]

        cond = tensordict[self.il_enabled_key][..., -1, 3].item()
        """ For environments that don't support take control, dimensions 4: won't be used """
        il_action = tensordict[self.il_action_key][..., -1, 4:4 + act_dims]

        out = cond * il_action + (1 - cond) * predicted_action

        action_tensordict.set(action_key, out)
        return tensordict
