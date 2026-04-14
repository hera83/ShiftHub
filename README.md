# ShiftHub

ShiftHub er et ASP.NET Core MVC-system til vagtplanlaegning, bemanding og intern koordinering. Systemet er bygget til at administrere grupper, brugere, medarbejdere, undergrupper og vagter, og det kombinerer planlaegning, registrering, beskeder og mailhaandtering i samme applikation.

Løsningen er nu delt i to niveauer:

- Roden indeholder solution-filen og workspace-konfiguration.
- Selve webapplikationen ligger i `web/`.

## Hvad systemet bruges til

ShiftHub bruges til at organisere medarbejdere i grupper og undergrupper, oprette vagtforloeb og holde styr på hvem der er tilmeldt, hvem der er checket ind, og om der mangler bemanding. Systemet har også en intern beskedmodel og en mailintegration, så koordinatorer kan kommunikere med medarbejdere direkte fra loesningen.

Set ud fra koden er systemet designet som en samlet driftsplatform til mindre eller mellemstore organisationer, hvor samme webapp både bruges til administration og den daglige operative planlaegning.

## Roller og adgang

Systemet bruger ASP.NET Core Identity med roller.

- `Administrator`: fuld adgang til administration af grupper og brugere.
- `InCharge`: gruppeansvarlig med ansvar for sin gruppe og dens planlaegning.
- Almindelig login-bruger: adgang til de almindelige moduler i systemet.

Hvis databasen er tom ved foerste opstart, bliver brugeren sendt til siden `Account/NoDb`, hvor den foerste administrator kan oprettes.

## Hovedmoduler

### 1. Konto og login

`AccountController` haandterer:

- login og logout
- adgangsfejl
- foerste systemopsaetning, hvis der endnu ikke findes brugere
- logning af login-forsøg i databasen

## 2. Administration

`AdminController` er for administratorer og indeholder blandt andet:

- oprettelse, redigering og sletning af grupper
- oprettelse, redigering og sletning af brugere
- tildeling af ansvarlige brugere til grupper
- eventuel oprettelse af mailopgaver ved oprettelse af grupper og brugere

Administration er derfor systemets centrale modul til etablering af organisationsstrukturen.

## 3. Medarbejdere

I `HomeController` findes et komplet medarbejdermodul med:

- oversigt over medarbejdere
- oprettelse, redigering og sletning
- import af medarbejdere
- eksport af medarbejdere
- tilknytning af brugerdefinerede felter som `Key1`, `Key2` og `Key3`

Medarbejdere gemmes i tabellen `Staff` og er knyttet til en gruppe.

## 4. Undergrupper og planlaegning

Systemets planlaegningsdel er bygget op omkring en raekke underliggende enheder:

- `SubGroup`: et planlaegningsområde eller delområde inden for en gruppe
- `SubGroupDay`: dage eller forloeb knyttet til en undergruppe
- `SubGroupShift`: konkrete vagter med start/slut, type og behov
- `SubGroupNeed`: bemandingsbehov på givne tidspunkter
- `SubGroupStaff`: relation mellem medarbejdere og undergrupper
- `SubGroupRegistration`: tilmeldinger til vagter

Der understoettes både faste og fleksible vagttyper.

Views som `Planner`, `Registration`, `SubShiftsView`, `SubDaysAdd`, `SubShiftsAdd` og `SubNeedsEdit` viser, at systemet både bruges til planlaegning, overblik og konkret drift.

## 5. Poster, check-in og check-out

Systemet indeholder også et operationelt lag til afvikling af vagter:

- `SubGroupPostGroup`: definerer poster eller funktioner
- `SubGroupPostMember`: knytter medarbejdere til poster
- check-in/check-out handlinger i `HomeController`
- registrering af alarmstatus og maksimal tid på poster

Det peger på, at ShiftHub ikke kun bruges til at bygge en plan, men også til at styre selve gennemfoerelsen af en vagt eller aktivitet.

## 6. Beskeder, notifikationer og alerts

Systemet har flere kommunikationsspor:

- `AppMessage`: interne systembeskeder til brugerfladen
- `SubGroupMessage`: beskeder relateret til undergrupper og medarbejdere
- `SubGroupAlert`: alarmer og opfoelgning i relation til bemanding og hændelser

Der findes views til at sende, besvare og vise beskeder, og notater i projektet peger samtidig på, at et mere samlet beskedcenter er et planlagt eller oensket næste skridt.

## 7. Formularer og tilmelding

Systemet indeholder et modul til formularaktivering og tilmelding:

- `SubGroupForm` gemmer formularindhold
- `SubFormActivate` og `SubFormElements` viser, at formularer kan aktiveres pr. undergruppe
- `Tilmelding` giver et offentligt eller delt tilmeldingsflow baseret på formular og medarbejderdata

Det indikerer, at ShiftHub kan bruges til at samle tilmeldinger eller registreringer omkring bestemte vagter eller forloeb.

## Mailintegration og baggrundsservices

Der er to hosted background services registreret ved opstart:

- `BgServiceMailSender`
- `BgServiceMailHandler`

De bruges til henholdsvis:

- afsendelse af mails
- haandtering af indgaaende mails og svar

Mailkonfiguration ligger i `web/appsettings.json` og forventer SMTP/IMAP-oplysninger. Koden er bygget op omkring en enkelt global mailkonto, og mailfunktionen kan slås til eller fra via `MailConfig:Active`.

Systemet bruger `MailKit` til IMAP og den indbyggede .NET mailklient til SMTP-afsendelse.

## Teknologi

- .NET 10
- ASP.NET Core MVC
- Entity Framework Core 10
- ASP.NET Core Identity
- SQLite
- Serilog
- MailKit

Applikationen er en klassisk monolit, hvor web, databaseadgang, baggrundsjob og views ligger i samme projekt.

## Data og persistence

Databasen er SQLite og konfigureret i `web/appsettings.json`:

`Data Source=App_Data/shifthub.db`

Ved opstart koeres EF Core-migrationer automatisk via `db.Database.Migrate()`, så databasen bliver oprettet eller opdateret ved runtime.

Vigtige dataområder i modellen:

- grupper og brugere
- loginlog
- medarbejdere
- undergrupper, dage og vagter
- bemandingsbehov og registreringer
- poster og bemandingsstatus
- beskeder, mails og alerts

## Logging

Serilog er sat op i `Program.cs` og skriver både til konsol og fil.

Logfiler rulles dagligt i mappen `Logs/`.

## Koer projektet lokalt

Byg fra roden:

```powershell
dotnet build ShiftHub.sln -nologo /consoleloggerparameters:NoSummary
```

Koer webprojektet:

```powershell
dotnet run --project web/ShiftHub.csproj
```

Standardprofiler i `launchSettings.json` bruger udviklings-URLs som blandt andet:

- `https://localhost:7013`
- `http://localhost:5035`

## Konfiguration

Foelgende konfiguration er central:

- `ConnectionStrings:DefaultConnection`
- `URL:HostName`
- `MailConfig:*`

For at mailfunktioner virker, skal SMTP- og IMAP-oplysninger udfyldes i `web/appsettings.json`.

## Kendte forhold i koden

Ud fra den nuvaerende kodebase er der nogle ting man boer kende:

- `SmsServices` findes, men er ikke implementeret endnu.
- Sletning af grupper ser ikke ud til at rydde alle underliggende undergruppe-data fuldt ud.
- Beskeder gemmes flere steder som Base64-enkodet indhold, hvilket er et designvalg i den nuvaerende loesning.
- UI og datobehandling er tydeligt praeget af dansk brug og dansk kulturformattering.

## Docker

Projektet kan koeres som Docker-container via de medfoegende konfigurationsfiler.

### Forudsaetninger

- Docker og Docker Compose installeret

### Kom i gang

**1. Opret `.env` fra eksemplet og udfyld mailoplysninger:**

```bash
cp .env.example .env
```

Redigér `.env` med dine SMTP/IMAP-oplysninger. `.env` maa aldrig committes.

**2. Byg og start containeren:**

```bash
docker compose up --build -d
```

Applikationen er herefter tilgaengelig paa `http://localhost:8080`.

**3. Stop containeren:**

```bash
docker compose down
```

### Data og persistens

Docker Compose monterer to lokale mapper som volumes:

| Host-sti | Container-sti | Indhold |
|---|---|---|
| `data/App_Data/` | `/app/App_Data/` | SQLite-databasefil (`shifthub.db`) |
| `data/Logs/` | `/app/Logs/` | Serilog-logfiler |

Mapperne oprettes automatisk ved foerste opstart. `data/` er ekskluderet fra git og Docker-image.

### Konfiguration via miljoevariable

Alle indstillinger fra `appsettings.json` kan overstyres med miljoevariable efter ASP.NET Core-konventionen (`__` erstatter `:`). F.eks.:

```
ConnectionStrings__DefaultConnection=Data Source=App_Data/shifthub.db
MailConfig__Username=minmail@example.com
```

Mailoplysninger angives i `.env`-filen (se `.env.example`).

### HTTPS og omvendt proxy

Containeren lytter paa HTTP port 8080. HTTPS-terminering boer haandteres af en omvendt proxy (f.eks. nginx eller Caddy) foran containeren i en produktion.

### Filer

| Fil | Formaal |
|---|---|
| `Dockerfile` | Multi-stage build: SDK til kompilering, ASP.NET runtime til afvikling |
| `docker-compose.yml` | Definerer service, porte, volumes og miljoevariable |
| `.env.example` | Skabelon til `.env` med mailkonfiguration |
| `.dockerignore` | Ekskluderer `bin/`, `obj/`, databse og logs fra image |

---

## Kort opsummering

ShiftHub er et samlet system til administration, vagtplanlaegning, bemanding, registrering og kommunikation. Koden viser et system, der både understoetter den indledende oprettelse af organisation og den daglige operative drift med medarbejdere, vagter, poster, beskeder og mailflows.