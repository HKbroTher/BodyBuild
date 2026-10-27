namespace MassFactory.Core
{
    public enum InjectionOutcome
    {
        BodyDestroyed = 0,
        Atrophy = 1,
        NoEffect = 2,
        Growth = 3,
        Mutation = 4
    }

    public static class InjectionOutcomeExtensions
    {
        public const int Count = 5;

        public static bool IsSuccess(this InjectionOutcome outcome)
        {
            return outcome == InjectionOutcome.Growth || outcome == InjectionOutcome.Mutation;
        }

        public static string ToLabel(this InjectionOutcome outcome)
        {
            switch (outcome)
            {
                case InjectionOutcome.Mutation: return "MUTATION";
                case InjectionOutcome.Growth: return "GROWTH";
                case InjectionOutcome.NoEffect: return "NO EFFECT";
                case InjectionOutcome.Atrophy: return "ATROPHY";
                default: return "BODY DESTROYED";
            }
        }
    }
}
