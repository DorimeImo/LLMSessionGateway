namespace LLMSessionGateway.Core.Utilities.Functional
{
    public class Result<T>
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string? Error { get; }
        public string? ErrorCode { get; }
        public bool IsRetryable { get; }
        public T? Value { get; }

        private Result(T value)
        {
            IsSuccess = true;
            Value = value;
        }

        private Result(string error, string? errorCode = null, bool isRetryable = false)
        {
            IsSuccess = false;
            Error = error ?? throw new ArgumentNullException(nameof(error));
            ErrorCode = errorCode;
            IsRetryable = isRetryable;
        }

        public static Result<T> Success(T value) => new(value);

        public static Result<T> Failure(string error, string? errorCode = null, bool isRetryable = false)
            => new(error, errorCode, isRetryable);

        public override string ToString()
        {
            return IsSuccess
                ? $"Success({Value})"
                : $"Failure({Error}) [Code={ErrorCode}, Retryable={IsRetryable}]";
        }
    }
}
