using System;

namespace Dagmay.Core.Contracts
{
    public sealed class OperationResult<T>
    {
        private readonly T _value;

        private OperationResult(bool isSuccess, T value, string errorCode, string errorMessage)
        {
            IsSuccess = isSuccess;
            _value = value;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public bool IsSuccess { get; }

        public string ErrorCode { get; }

        public string ErrorMessage { get; }

        public T Value
        {
            get
            {
                if (!IsSuccess)
                {
                    throw new InvalidOperationException("A failed result has no value.");
                }

                return _value;
            }
        }

        public static OperationResult<T> Success(T value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return new OperationResult<T>(true, value, string.Empty, string.Empty);
        }

        public static OperationResult<T> Failure(string errorCode, string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorCode))
            {
                throw new ArgumentException("An error code is required.", nameof(errorCode));
            }

            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException("An error message is required.", nameof(errorMessage));
            }

            return new OperationResult<T>(false, default!, errorCode, errorMessage);
        }
    }
}

