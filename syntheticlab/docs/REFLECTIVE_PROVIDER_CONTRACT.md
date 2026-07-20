# Reflective Provider Contract

A real model provider must sit behind `ReflectiveGateway`.

Required audit fields:
- provider ID;
- exact model ID;
- run timestamp;
- prompt ID/version/hash;
- evidence IDs;
- request hash;
- response hash;
- validated proposal hash;
- settings;
- code version;
- branch ID.

Rules:
- do not request hidden chain-of-thought;
- store concise rationale only;
- every self-hypothesis proposal cites available evidence IDs;
- model text cannot become canonical fact;
- provider output cannot directly mutate identity, evidence, relationship history, or skills;
- prompt changes create a new experimental mechanism version;
- provider/model changes create an explicit experimental intervention.
