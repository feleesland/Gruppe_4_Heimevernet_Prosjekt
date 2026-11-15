namespace Heimevernet_Prosjekt.Models.Entities
{
    // The status of a need, like in the case: Ny -> Vurderes -> Tildelt -> Løst.
    // Not saved in the database: worked out from the requests every time, so it is always right (help with AI).
    // How much is covered is shown next to the status ("Vurderes · 2/4"), like "3 av 5" in Jira
    public enum NeedStatus
    {
        Ny,           // nobody asked yet
        Vurderes,     // somebody is asked, but not everything is covered yet (also when some said yes)
        Tildelt,      // everything is covered: every line has enough that said yes
        LeverTilbake, // closed, but resources are not returned yet
        Lost          // closed, and everything is back ("Løst")
    }

    public static class NeedStatusRules
    {
        public static NeedStatus For(ResourceNeed need, IEnumerable<ResourceRequest> requests)
        {
            var list = requests.ToList();

            // Closed: «Løst» only when everything is back with the owners
            if (need.ClosedAt != null)
                return list.Any(r => r.Status == RequestStatus.Godtatt && r.ReturnedAt == null) ? NeedStatus.LeverTilbake : NeedStatus.Lost;

            var (covered, quantity) = Coverage(need, list);
            if (quantity > 0 && covered >= quantity) return NeedStatus.Tildelt;
            if (list.Any(r => r.Status != RequestStatus.Avbrutt)) return NeedStatus.Vurderes;
            return NeedStatus.Ny;
        }

        // How many said yes (never more than each line asks for) and how many are needed in all, e.g. 2 and 4
        public static (int Covered, int Quantity) Coverage(ResourceNeed need, IEnumerable<ResourceRequest> requests)
        {
            var list = requests.ToList();
            var covered = need.Items.Sum(item => Math.Min(item.Quantity,
                list.Count(r => r.ResourceNeedItemId == item.Id && r.Status == RequestStatus.Godtatt)));
            return (covered, need.Items.Sum(item => item.Quantity));
        }
    }
}