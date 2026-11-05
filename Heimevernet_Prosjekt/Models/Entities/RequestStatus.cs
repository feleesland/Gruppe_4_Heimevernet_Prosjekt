namespace Heimevernet_Prosjekt.Models.Entities
{
    // Where a request is. "Ikke svart" is not stored: it is Sendt after AnswerBy (help with AI)
    // Stored as a number, so new values must be added at the end
    public enum RequestStatus
    {
        Sendt,
        Godtatt,
        Avslatt,
        IkkeLengerNodvendig,
        Avbrutt
    }
}
