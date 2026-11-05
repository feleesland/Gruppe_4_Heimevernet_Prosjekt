# Gruppe 4 , Kartverket/Heimevernet

Dette er gruppe 4 sitt repo for 3. semester prosjektet i IS-20x.
Prosjektet går ut på å lage en webapplikasjon med kart som skal hjelpe til med å håndtere krisesituasjoner gjennom å registrere tilgjengelige ressurser og deres posisjoner.

#
### Drift

Applikasjonen vår køyrast på .NET 10 og brukar dermed.NET Aspire til starting og samordning av nettløysinga og databasen. 
Docker må vere i gang ved lokal køyring, grunnen er at MariaDB køyrast i ein container. 
Heimevernet_Prosjekt.AppHost: her blir prosjektet starta og status , loggar og lenker til nettsida visast gjennom Aspire-dashbordet. 
Vi har tatt i bruk av Docker-volum for databasen vår, det vil sei at data blir bevart sjølv om vi starta containeren på nytt igjen, i tillegg Entity Framework Core-migrering sørge for oppdatering av databasestrukturen ved applikasjon start. Testkontoar og testressursar blir også oppretta og lagt til i utviklingsmiljøet våt. 
E-post blir skrevet til loggen når ein jobbar i lokal utvikling. Utanfor utviklingsmiljøet vårt er SendGrid satt opp e-post teneste og dermed krevje ein API-nøkkel. 
Produksjonsutrulling og rutinar for sikkerheitskopiering er nok ikkje dokumentert i grunnlaget som er no gjennomgått. Eit Docker-volum vil nok ikkje erstatte ein sikkerheitskopiering. 

...

#
### System arkitektur
Systemarkitektur
Systemet vårt er ein ASP.NET Core MVC-applikasjon. Det handterer ressursar, ressursbehov, ressurstypar. Ressursrapportar og til slutt brukarar. 
Komponent	Bruk
Views og viewmodels	Razor-visning presentere nettsidene. Viewmodels flytta og sjekke skjemaopplysning. Bootstrap og CSS gjer det mogleg for visning på forskjellige skjerm størrelse. 
Controllers	Tar i mot førespurnader, behandle brukarhandling og velje kva visning og svar må returnerast.  
Repository og database	Repositories samla datatilgang, Entity Framcwork Core lese og lagre data i MariaDB databasen vår gjennom HeimevernetDbContext. 
Innlogging og tilgang	ASP.NET Core identity ta seg av brukarkonto og innlogging. Rolle og autorisasjonsreglar styre tilgangen. 
Kart og tenester 	Leaflet brukast til visning av kart og markør. Kartverket-tenesta bidra med søking av adresser og stader. 

Ein typisk førespurnad går frå nettlesaren til ein controllar, dermed til eit repository og databasen. Controlleran sende dei nødvendige data til visning eller returnere JSON, kartmarkørar er eit eksempel på det. 
For ressursar gir nok autorisasjonshandteraren administrator tilgang, eigaren får tilgang til eigne ressursar og operatøren får lesetilgang. Ressurs og ressursbehov følgje ein logisk sletting, det vil sei at dei blir skjulte ved databasefilter og oppføringane blir ivaretatt i databasen vår. 


Databasen...
#
### Testing scenarioer og resultater
Unit tester er lagt til flere på vei, startet på med KI og gjennomgått før oppdatering av branch

#
### Vår bruk av KI i prosjektet

KI verktøyene gruppen har brukt har vært blant annet GitHub Copilot Chat, ChatGPT, og Anara.

Disse har blitt brukt på følgende måter:

- GitHub Copilot Chat har vært til hjelp med å debugge og finne løsninger på problemer som har oppstått underveis. Inklusivt brukt for å raskt lage unit-tester før de ble sjekket av gruppe medlem.
- ChatGPT har blitt brukt for å få rask hjelp med småting som "Hvordan å endre at kartstørrelsen tilpasser seg skjermer automatisk". Den har da kommet med forslag til måter dette kunne gjøres på.
- I Anara har tutoriels fra IS-202 som er brukt i dette prosjektet blitt lagt inn for å få bedre forståelse av hvordan koden fungerer ved å snakke med filene.
- Bruk av KI til å navigere mellom forskjellige mappar og instillingar i visual studio. 

...

#
### Info om hvordan applikasjonen kjøres lokalt
For å kjøre applikasjonen lokalt, følg disse trinnene:

