namespace MassFactory.Core
{
    public class ActionResult
    {
        public bool Success { get; protected set; }
        public string Error { get; protected set; }

        public static ActionResult Ok() { return new ActionResult { Success = true }; }
        public static ActionResult Fail(string error) { return new ActionResult { Success = false, Error = error }; }
    }

    public sealed class ActionResult<T> : ActionResult
    {
        public T Value { get; private set; }

        public static ActionResult<T> Ok(T value) { return new ActionResult<T> { Success = true, Value = value }; }
        public static new ActionResult<T> Fail(string error) { return new ActionResult<T> { Success = false, Error = error }; }
    }
}
