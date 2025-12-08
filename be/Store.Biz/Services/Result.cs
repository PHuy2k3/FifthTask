namespace Store.Biz.Services
{
    public class Result
    {
        public bool IsSuccess { get; private set; }
        public string? Error { get; private set; }

        public static Result Ok() => new Result { IsSuccess = true };
        public static Result Fail(string error) => new Result { IsSuccess = false, Error = error };
    }
}
