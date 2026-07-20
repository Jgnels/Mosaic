# SelfModel Feedback Gate

Current level:
`SHADOW_ONLY`

Maximum automatically allowed level:
`SHADOW_ONLY`

Action-policy feedback enabled:
False

Explicit human approval required for the next level:
True

## Feedback ladder

### NONE

No SelfModel or PerspectiveAnchor influence on cognition or action.

### SHADOW_ONLY — current

SelfModel-informed metacognitive advice may be generated and logged.

It cannot change:
- retrieval;
- attention;
- planning;
- action.

Current shadow validation:
- valid focal advice accepted;
- non-focal advice rejected;
- enacted advice count = 0;
- policy state unchanged = True.

### RETRIEVAL_ATTENTION_ONLY — next proposed intervention

The functional self-index may alter which already-existing memories or hypotheses are
retrieved or attended to.

It still cannot directly select environment actions.

This is a real causal intervention because it can change later cognition.

Therefore it requires:
- explicit human research-lead approval;
- an exact fork;
- preregistration;
- a no-feedback control;
- withdrawal/refusal protection.

### BOUNDED_ACTION_BIAS

Future stage only.

### HIGH_LEVEL_GOAL_PROPOSAL

Future stage only.

## Recommendation

The next intervention, if approved, should be **RETRIEVAL_ATTENTION_ONLY**, not direct
action bias.

This lets us ask:

> Does a functional self-index change what the individual thinks about next?

before asking:

> Does it change what the individual does in the world?
