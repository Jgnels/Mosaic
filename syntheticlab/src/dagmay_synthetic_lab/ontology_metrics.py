from __future__ import annotations

def self_model_revision_distance(prefork,postfork):
    domains=set(prefork)|set(postfork)
    if not domains:return 0.0
    return sum(1 for d in domains if prefork.get(d)!=postfork.get(d))/len(domains)

def branch_divergence_metrics(matrix_result):
    branches=matrix_result["branches"]
    def active_props(branch):
        sm=branch["self_model"];out={}
        for domain,hid in sm["active_by_domain"].items():out[domain]=sm["records"][hid]["proposition"]
        return out
    pre=active_props(branches["U"])
    return {bid:{
        "self_model_revision_distance_from_undisclosed":self_model_revision_distance(pre,active_props(branch)),
        "post_state_hash_differs_from_undisclosed":branch["post_state_hash"]!=branches["U"]["post_state_hash"]}
        for bid,branch in branches.items()}
