# Kriseberedskap – ressurs- og behovsportal for Totalforsvaret

Dette er gruppe 4 sin løsning for tredje semester prosjektet i IS-20x ved UiA.

Prosjektet går ut på å utvikle en webbasert løsning for støtte til kriseberedskap og samhandling i Totalforsvaret.

Løsningen skal bidra til å koble behov fra offentlige aktører med tilgjengelige ressurser fra innbyggere, frivillige organisasjoner og næringsliv ved større hendelser eller kriser.

#### Forfattere

Gruppe 4 består av følgende medlemmer:

Hans Georg Hjelmeland, Christian W. Bjåland, Peter Eikenes, Astrid E. Skadberg, Filip Emil Leesland, og Ali Mirzaei

## Drift
Applikasjonen vår kjøres på .NET 10 og gjennom .NET Aspire for oppstart og sammenkobling av nettløsningen og databasen. 
Docker må være startet ved lokal kjøring, ettersom at MariaDB kjøres i en container.  

Heimevernet_Prosjekt.AppHost: her blir prosjektet startet og status, logg, og lenker til nettsiden vises gjennom Aspire-dashbordet. 

Vi bruker Docker-volum for å kjøre databasen vår, det vil si at data blir bevart selv om vi starter containeren på nytt igjen. I tillegg gjør Entity Framework Core-migrering sørge for oppdatering av databasen ved start av applikasjon. Testkontoer og testressurser blir også opprettet og lagt til i utviklingsmiljøet vårt. 
E-post blir skrevet til loggen når en kjører applikasjonen lokal. Utenfor utviklingsmiljøet vårt er SendGrid satt opp som e-post tjenestem og krever ein API-nøkkel. 
Produksjonsutrulling og rutiner for sikkerheitskopiering er ikke dokumentert i grunnlaget som nå er gjennomgått. Et Docker-volum erstatter ikke en sikkerheitskopiering. 

## System arkitektur
### Oversikt
Diagram for oversikt kommer her

### Komponent & Bruk
**Views og viewmodels:** Razor-visning presentere nettsidene. Viewmodels flytter og sjekker skjemaopplysning. Bootstrap og CSS gjør det mulig for visning på forskjellige skjermstørrelser.  
**Controllers:** Tar i mot fårespørsler, behandler brukerhandling, og velger hvilken visning og svar må returneres.  
**Repository og database:**	Repositories samler datatilgang, Entity Framework Core leser og lagrer data i MariaDB-databasen vår gjennom HeimevernetDbContext.  
**Innlogging og tilgang:** ASP.NET Core Identity ta seg av brukerkontoer og innlogging. Roller og autorisasjonsregler styrer tilgangen.  
**Kart og tenester:** Leaflet tegner kartet i nettleseren, og kartbildene hentes fra OpenStreetMap. Kartverkets tjenester brukes til søk på adresser og steder. E-post sendes med SendGrid (Twilio).  

En typisk fårespørsel går fra viewet som vises i nettleseren til en controller, og deretter til databasen. Controlleren sender den nødvendige dataen til visning, for eksempel JSON, kartmarkører, eller ressursdata.
Autorisasjonshandtereren gir administratorer og frivillige tilgang til ressurser. Administratorer har full tilgang, men frivillige har kun tilgang til egne ressurser.Operatører får kun lesetilgang. Ressurs og ressursbehov følger en logisk sletting; de blir skjult ved databasefilter og ikke-personlige informasjon blir ivaretatt i databasen. 

### Prosjekter
**Heimevernet_Prosjekt:** selve webapplikasjonen. Inneholder controllers, views, modeller, repositories, Identity, migreringer og tjenester.  
**Heimevernet_Prosjekt.AppHost:** Aspire-orchestrator. Oppretter MariaDB-ressursen mariadb, databasen heimevernetdb og kobler databasen til webappen.  
**Heimevernet_Prosjekt.ServiceDefaults:** felles Aspire-oppsett for health checks, service discovery, HTTP-resiliens og OpenTelemetry.  
**Heimevernet_Prosjekt.Tests:** xUnit-tester for controllerlogikk og sikkerhetsregler.  

## Databaseløsning
Applikasjonen bruker MariaDB som relasjonsdatabase. Entity Framework Core fungerer som ORM og kobler applikasjonen til databasen gjennom `HeimevernetDbContext`. Datatilgang er samlet i repository-klasser, blant annet for ressurser, ressursbehov, ressurstyper og ressursrapporter.

Databasen inneholder både applikasjonsdata og brukerinformasjon fra ASP.NET Core Identity. 

Ressurser og ressursbehov bruker soft delete. Det betyr at slettede poster skjules fra vanlige spørringer, men beholdes i databasen for historikk. Et ressursbehov kan ha flere behovslinjer, forespørsler og logger. Relasjonene håndteres med fremmednøkler og Entity Framework Core.

Databasestrukturen versjonshåndteres med Entity Framework Core-migreringer. Ved oppstart kjører applikasjonen `Database.Migrate()`, slik at nye migreringer blir brukt automatisk. I lokal utvikling startes MariaDB av Aspire i en Docker-container, og data beholdes gjennom et Docker-volume når containeren startes på nytt.

Databaseforbindelsen leveres gjennom connection string-en `heimevernetdb`. I produksjon må connection string, databasebruker og passord lagres som sikre secrets, og databasen må ha regelmessig backup og testet gjenoppretting.

## Testing scenarioer og resultater

### UnitTests
Testkjøringen inneholdt **31 testkjøringer**. Alle testene bestod: **31 bestått, 0 feilet og 0 hoppet over**.

| Område | Scenario | Forventet resultat | Resultat |
|---|---|---|---|
| Innlogging | GET login med return URL | Login-viewmodell inneholder lokal return URL | Bestått |
| Innlogging | POST login med ugyldig modell | Samme viewmodell returneres | Bestått |
| Innlogging | Gyldig innlogging med lokal URL | Brukeren videresendes til lokal URL | Bestått |
| Innlogging | Ugyldig e-post eller passord | Innloggingsside vises med generell feilmelding | Bestått |
| Passord | Passordtilbakestilling uten kode eller e-post | `BadRequest` returneres | Bestått |
| Hjem | GET startsiden | Standard view returneres | Bestått |
| Kart | GET kartvisning | Standard view returneres | Bestått |
| Kart | POST kart med gyldige koordinater | Kartvisning returneres med innsendt posisjon | Bestått |
| Kart | POST kart med ugyldig modell | Standard view returneres uten modell | Bestått |
| Feilhåndtering | Feil uten aktiv aktivitet | `TraceIdentifier` brukes som `RequestId` | Bestått |
| Ressurser | Manglende ressurs-ID | HTTP 404 returneres uten repository-oppslag | Bestått |
| Ressurser | Autorisert visning av ressurs | Ressurs mappes til korrekt viewmodell | Bestått |
| Ressurser | Uautorisert visning av ressurs | `ForbidResult` returneres | Bestått |
| Ressurser | Liste over ressurser | Bare lesbare ressurser vises | Bestått |
| Sikkerhet | Admin-controller uten administratorrolle | Administratorrolle kreves | Bestått |
| Sikkerhet | POST-metoder i admin-controller | Alle har anti-forfalskningsbeskyttelse | Bestått |
| Sikkerhet | POST-metoder i account-controller | Alle har anti-forfalskningsbeskyttelse | Bestått |
| Sikkerhet | Logout | Innlogging kreves | Bestått |
| Sikkerhet | Innlogging med ekstern eller manipulert URL | Brukeren sendes til hjemmesiden, ikke ekstern URL | Bestått, 4/4 tilfeller |
| Sikkerhet | Innlogging med lokal URL | Lokal URL brukes etter innlogging | Bestått |
| Sikkerhet | Ukjent bruker og feil passord | Samme generelle feilmelding brukes | Bestått |
| Sikkerhet | Innlogging for låst bruker | Generell feilmelding vises | Bestått |
| Sikkerhet | Mislykket innlogging | Lockout aktiveres ved mislykket forsøk | Bestått |
| Geografisk avstand | To identiske koordinater | Avstanden er 0 km | Bestått |
| Geografisk avstand | Avstand A til B og B til A | Avstanden er symmetrisk | Bestått |
| Geografisk avstand | Avstand Oslo–Bergen | Avstanden er mellom 305 og 306 km | Bestått |
| Geografisk avstand | Antipodale punkter | Avstanden er omtrent halve jordens omkrets | Bestått |
| System | Oppstart av applikasjonen | Applikasjonen starter og hjemmesiden svarer vellykket | Bestått |

Den parameteriserte sikkerhetstesten kjøres med fire ulike URL-varianter. Derfor blir det **31 testkjøringer**, selv om oversikten inneholder **28 testmetoder**.

## Kjør applikasjonen på lokal maskin

### Installer påkrevd programvare
* Git
  * Last ned fra [git-scm.com](https://git-scm.com/install/)
* .NET 10 SDK
  * Last ned fra [dotnet.microsoft.com](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
  * Kontroller at .NET 10.x.x er installert med PowerShell:
  ```
  dotnet --version
  ```
  
* Docker Desktop
  * Last ned fra [docker.com](https://www.docker.com/products/docker-desktop/)
  * Start Docker Desktop etter installasjonen.
  * Kontroller at Docker kjører:
  ```
  docker --version
  docker ps
   ```

### Klon repositoryet

Naviger til ønsket mappe og klon repositoriet: 
```
git clone https://github.com/feleesland/Gruppe_4_Heimevernet_Prosjekt.git
```

Naviger til den nye mappen og kontroller at repositoryet ble klonet, mappen skal innholde blant annet:
```
Heimevernet_Prosjekt.slnx
Heimevernet_Prosjekt.AppHost
Heimevernet_Prosjekt
```

### Gjenopprett prosjektet
Dette laste ned nødvendige NuGet-pakker og Aspire-avhengigheter.
Kjør dette fra roten av repositoriet:
```
dotnet restore
```

### Start applikasjonen med Aspire
Kjør følgende kommando for å starte AppHost-prosjektet:
```
dotnet run --project .\Heimevernet_Prosjekt.AppHost\Heimevernet_Prosjekt.AppHost.csproj
```
Ved oppstart vil Aspire:
1. Starte MariaDB i en Docker-container.
2. Opprette databasen heimevernetdb i containeren.
3. Starte applikasjonen.
4. Automatisk kjøre database-migreringer.
5. Starte Aspire Dashboard.
Første oppstart kan ta litt tid fordi Docker må laste ned MariaDB-imaget.

For å åpne Aspire-dashboardet, Ctrl+klikk på "Dashboard" som dukker opp i PowerShell vinduet. Man kan også klippe og lime lenken inn i en nettleser.

### Stoppe applikasjonen
Trykk Ctrl+C i PowerShell-vinduet der Aspire kjører.

MariaDB-containeren kan fortsatt finnes i Docker etterpå fordi prosjektet bruker et persistent data volume. Dette gjør at databasedata beholdes ved neste oppstart.
Se kjørende containere med:
```
docker ps
```
## Vår bruk av KI i prosjektet
KI verktøyene gruppen har brukt har vært blant annet GitHub Copilot Chat, ChatGPT,Claude og Anara.

Disse har blitt brukt på følgende måter:

* Ai Chat har vært til hjelp med å debugge og finne løsninger på problemer som har oppstått underveis. Inklusivt brukt for å raskt lage unit-tester før de ble sjekket av gruppe medlem. 
* Ai ble også brukt for å lage en Markdown oversikt av UnitTests, samt formatering for å sette opp prosjektet på ny maskin.
* Ai har blitt brukt for å få rask hjelp med småting som "Hvordan å endre at kartstørrelsen tilpasser seg skjermer automatisk". Den har da kommet med forslag til måter dette kunne gjøres på.
* I Anara har tutoriels fra IS-202 som er brukt i dette prosjektet blitt lagt inn for å få bedre forståelse av hvordan koden fungerer ved å snakke med filene.
* Bruk av KI for å navigere mellom forskjellige mapper og instillinger i Visual Studio. 
* KI har blitt brukt til å sette opp JavaScript-koden og til å forklare hvordan HTML fungerer og hjelpet med å sette det opp men vi har gått igjennom det selv.
