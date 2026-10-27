namespace MassFactory.Core
{
    /// <summary>Generates absurd fictional stage names. Deterministic for a given random source.</summary>
    public static class AthleteNameGenerator
    {
        private static readonly string[] FirstNames =
        {
            "Brock", "Chad", "Dolph", "Thor", "Rex", "Vince", "Tank", "Grizz", "Duke", "Moose",
            "Brick", "Lance", "Hank", "Biff", "Rocco", "Sven", "Dmitri", "Kurt", "Magnus", "Gus"
        };

        private static readonly string[] Nicknames =
        {
            "The Fridge", "Vein Train", "Protein Shake", "Two Hearts", "Deltoid Prime", "Quadzilla",
            "The Lab Rat", "Sir Pump", "Mr. Mass", "The Wardrobe", "Kidney Stone", "Big Batch",
            "Side Effect", "The Experiment", "Gym Ghost", "Batch 404", "Chicken Legs",
            "Unit 7", "Neckless", "The Forklift"
        };

        private static readonly string[] LastNames =
        {
            "Hammerstein", "Ironside", "Bulkowski", "McFlex", "Grunthorpe", "Steelman", "Vasquez-Bulk",
            "Ox", "Gainsworth", "Liftberg", "Pumpernickel", "Slabsky", "Torque", "Krank", "Musclewhite"
        };

        public static string Generate(IRandomSource rng)
        {
            string first = FirstNames[rng.Next(FirstNames.Length)];
            string nick = Nicknames[rng.Next(Nicknames.Length)];
            string last = LastNames[rng.Next(LastNames.Length)];
            return first + " \"" + nick + "\" " + last;
        }
    }
}
