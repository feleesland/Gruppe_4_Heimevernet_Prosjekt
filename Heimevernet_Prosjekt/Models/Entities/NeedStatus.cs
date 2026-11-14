namespace Heimevernet_Prosjekt.Models.Entities
{
    // The status of a need, like in the case: Ny -> Vurderes -> Tildelt -> Løst.
    // Not saved in the database: worked out from the requests every time, so it is always right (help with AI)
    public enum NeedStatus
    {
        Ny,        // nobody asked yet
        Vurderes,  // requests sent, waiting for an answer
        Tildelt,   // at least one said yes
        Lost       // closed ("Løst")
    }

    public static class NeedStatusRules
    {
        public static NeedStatus For(ResourceNeed need, IEnumerable<ResourceRequest> requests)
        {
            if (need.ClosedAt != null) return NeedStatus.Lost;
            if (requests.Any(r => r.Status == RequestStatus.Godtatt)) return NeedStatus.Tildelt;
            if (requests.Any(r => r.Status == RequestStatus.Sendt)) return NeedStatus.Vurderes;
            return NeedStatus.Ny;
        }
    }
}