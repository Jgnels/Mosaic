using Dagmay.Core.Contracts;
using Dagmay.Core.Identity;

namespace Dagmay.Core.Abstractions
{
    public enum IdentityWriteStatus
    {
        Succeeded,
        AlreadyExists,
        NotFound,
        StaleVersion,
        IdentityMismatch,
        LineageMismatch,
        InvalidVersionStep
    }

    public interface IIdentityStore
    {
        bool TryGet(IndividualId id, out IndividualState? state);

        IdentityWriteStatus TryAdd(IndividualState state);

        IdentityWriteStatus TryReplace(long expectedVersion, IndividualState replacement);
    }
}

