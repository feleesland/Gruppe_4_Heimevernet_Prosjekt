# Gruppe 4 , Kartverket/Heimevernet
Dette er gruppe 4 sitt repo for 3. semester prosjektet i IS-20x.
Prosjektet går ut på å lage en webapplikasjon med kart som skal hjelpe til med å håndtere krisesituasjoner gjennom å registrere tilgjengelige ressurser og deres posisjoner.

## Drift
Applikasjonen vår kjøres på .NET 10 og bruker dermed .NET Aspire til oppstart og sammenkobling av nettløsningen og databasen. 
Docker må være startet ved lokal kjøring, ettersom at MariaDB kjøres i en container. 
Heimevernet_Prosjekt.AppHost: her blir prosjektet startet og status, logg, og lenker til nettsiden vises gjennom Aspire-dashbordet. 
Vi bruker Docker-volum for å kjøre databasen vår, det vil si at data blir bevart selv om vi starter containeren på nytt igjen. I tillegg gjør Entity Framework Core-migrering sørge for oppdatering av databasen ved start av applikasjon. Testkontoer og testressurser blir også opprettet og lagt til i utviklingsmiljøet vårt. 
E-post blir skrevet til loggen når en kjører applikasjonen lokal. Utenfor utviklingsmiljøet vårt er SendGrid satt opp som e-post tjenestem og krever ein API-nøkkel. 
Produksjonsutrulling og rutiner for sikkerheitskopiering er ikke dokumentert i grunnlaget som nå er gjennomgått. Et Docker-volum erstatter ikke en sikkerheitskopiering. 

## System arkitektur
Systemet vårt er en ASP.NET Core MVC-applikasjon. Det handterer ressurser, ressursbehov, ressurstyper, ressursrapportar, og brukerer. 

### Komponent & Bruk
Views og viewmodels: Razor-visning presentere nettsidene. Viewmodels flytter og sjekker skjemaopplysning. Bootstrap og CSS gjør det mulig for visning på forskjellige skjermstørrelser. 
Controllers: Tar i mot fårespørsler, behandler brukerhandling, og velger hvilken visning og svar må returneres.  
Repository og database:	Repositories samler datatilgang, Entity Framework Core leser og lagrer data i MariaDB-databasen vår gjennom HeimevernetDbContext. 
Innlogging og tilgang: ASP.NET Core Identity ta seg av brukerkontoer og innlogging. Roller og autorisasjonsregler styrer tilgangen. 
Kart og tenester: Leaflet brukes til visning av kart og markører. Kartverkets tjenester bidrar med søk på adresser og steder. 

En typisk fårespørsel går fra viewet som vises i nettleseren til en controller, og deretter til databasen. Controlleren sender den nødvendige dataen til visning, for eksempel JSON, kartmarkører, eller ressursdata.
Autorisasjonshandtereren gir administratorer og frivillige tilgang til ressurser. Administratorer har full tilgang, men frivillige har kun tilgang til egne ressurser.Operatører får kun lesetilgang. Ressurs og ressursbehov følger en logisk sletting; de blir skjult ved databasefilter og ikke-personlige informasjon blir ivaretatt i databasen. 


## Databasen
...

## Testing scenarioer og resultater
Unit tester er lagt til, med flere på vei. Startet på med KI og gjennomgått før oppdatering av branch


## Vår bruk av KI i prosjektet
KI verktøyene gruppen har brukt har vært blant annet GitHub Copilot Chat, ChatGPT, og Anara.

Disse har blitt brukt på følgende måter:

- GitHub Copilot Chat har vært til hjelp med å debugge og finne løsninger på problemer som har oppstått underveis. Inklusivt brukt for å raskt lage unit-tester før de ble sjekket av gruppe medlem.
- ChatGPT har blitt brukt for å få rask hjelp med småting som "Hvordan å endre at kartstørrelsen tilpasser seg skjermer automatisk". Den har da kommet med forslag til måter dette kunne gjøres på.
- I Anara har tutoriels fra IS-202 som er brukt i dette prosjektet blitt lagt inn for å få bedre forståelse av hvordan koden fungerer ved å snakke med filene.
- Bruk av KI for å navigere mellom forskjellige mapper og instillinger i Visual Studio. 

## Info om hvordan applikasjonen kjøres lokalt
For å kjøre applikasjonen lokalt, følg disse trinnene:
