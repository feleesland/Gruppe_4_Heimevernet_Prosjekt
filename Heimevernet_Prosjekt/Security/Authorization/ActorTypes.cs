namespace Heimevernet_Prosjekt.Security.Authorization
{
    public static class ActorTypes
    {
        public static readonly string[] Kinds = ["Privatperson", "Bedrift", "Frivillig organisasjon"];
        public static readonly string[] Skills = ["Bonde", "Entreprenør", "Droneoperatør"];
        public static readonly string[] OperatorKinds =
            ["Kommune", "Politi", "Brannvesen", "Sivilforsvaret", "Helse og ambulanse", "Frivillig beredskap", "Annet"];

        private static readonly (string Kind, string Skill)[] Blocked =
        [
            ("Frivillig organisasjon", "Bonde"),
        ("Frivillig organisasjon", "Entreprenør"),
    ];

        public static string[] KindsFor(bool isOperator) => isOperator ? OperatorKinds : Kinds;

        public static string[] Split(string? stored) =>
            string.IsNullOrEmpty(stored) ? [] : stored.Split(';', StringSplitOptions.RemoveEmptyEntries);

        public static string? Validate(string? kind, IEnumerable<string> skills, bool isOperator)
        {
            var skillList = skills.Distinct().ToList();

            if (!string.IsNullOrEmpty(kind) && !KindsFor(isOperator).Contains(kind))
                return "Ugyldig valg.";

            if (isOperator)
                return skillList.Count > 0 ? "Ugyldig valg." : null;

            if (skillList.Any(s => !Skills.Contains(s)))
                return "Ugyldig valg.";

            if (skillList.Count > 0 && string.IsNullOrEmpty(kind))
                return "Velg først om du er privatperson, bedrift eller frivillig organisasjon.";

            foreach (var skill in skillList)
                if (Blocked.Contains((kind ?? "", skill)))
                    return $"{kind} kan ikke samtidig være {skill.ToLower()}.";

            return null;
        }
    }
}
