# SyntheticLab Research Governance

## Confirmatory vs exploratory

A study is **confirmatory** only when:
- hypothesis/null are written before the run;
- primary metrics are frozen;
- seed count and stopping rule are frozen;
- exclusions are defined;
- analysis path is frozen.

A study is **exploratory** when any of those are changed after inspecting results.

Exploratory work is valuable but must be labeled honestly.

## Mechanism versioning

Any change to:
- perception filtering;
- salience/admission;
- event segmentation;
- memory retrieval;
- replay;
- consolidation;
- appraisal;
- reflection prompts;
- model provider;
- action policy;

creates a new mechanism version.

## Provenance rule

Derived artifacts must cite parents. Canonical events have no derived parent.

## Counterfactual replay

Reprocessing old evidence through a new mechanism creates a counterfactual result. It must never overwrite the record of what the individual actually believed or learned historically.

## Blinding

Where human evaluation is used:
- branch labels should be randomized;
- model/provider identity should be hidden where practical;
- evaluators should not see the experimental condition before scoring.

## Negative results

Null and negative findings are retained in the experiment registry.
