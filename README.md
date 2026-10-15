# Bildoptimerare

Komprimerar bilder för webb. Bara bilder som faktiskt blir mindre utan att bli sämre ändras —
övriga lämnas orörda.

## Vad som körs hos kund

`ImageOptimizer\publish\ImageOptimizer.exe` (svenska) eller
`ImageOptimizer.NO\publish\Bildeoptimalisering.exe` (norska).

Kopiera **enbart exe-filen** till kundens dator och dubbelklicka. Den är self-contained:
.NET behöver inte vara installerat, inga följefiler krävs, ingen installation.

Inställningarna sparas i `settings.json` bredvid exe-filen när mappen är skrivbar, annars under
`%APPDATA%\ImageOptimizer`. Det gör att en exe på en USB-sticka bär med sig sina inställningar,
medan en exe i Program Files ändå fungerar.

Bygga om:

```
dotnet publish ImageOptimizer\ImageOptimizer.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ImageOptimizer\publish
```

## Två lägen

**Kopiera till ny mapp** — originalen rörs inte. Destinationen blir en komplett spegling: bilder
som inte kunde förbättras kopieras oförändrade dit, så inget saknas.

**Skriv över originalen** — ersätter bilderna på plats, med valfri säkerhetskopia till en egen
mapp som speglar filnamn och mappstruktur. Kryssa i *Testkörning* först för att se vad som skulle
hända utan att någon fil ändras.

Undermappar tas alltid med, och mappstrukturen bevaras.

## Varför en bild lämnas orörd

| Orsak | Förklaring |
|---|---|
| Redan på eller under målkvaliteten | JPEG-kvaliteten läses ur kvantiseringstabellerna. Att koda om en q70-bild till q75 ger bara ännu en generation av förluster. |
| För liten vinst | Nya filen är inte minst N % mindre (förval 10 %). |
| Animerad | En omkodning skulle kasta alla bildrutor utom den första. |
| EXIF-rotation | Rotationen följer inte med omkodningen, och bilden skulle visas vriden. Se nedan. |
| Format som inte kan komprimeras säkert | BMP, GIF och TIFF. System.Drawing kan inte göra dem mindre utan att riskera kvaliteten. |

Filnamn och filändelse ändras aldrig, så referenser från html-sidor fortsätter peka rätt.
Genomskinlighet i PNG plattas inte mot vit botten.

PNG klarar inte exe-versionen — System.Drawing saknar styrning av PNG-komprimeringen, så
omkodningen blir lika stor eller större och bilden lämnas orörd. Skriptet gör det i stället
med `oxipng` och `pngquant`, se nedan.

## Skriptet

`Compress-Images.ps1` gör samma sak från kommandoraden, med samma regler. Kräver inget bygge.

```powershell
# Se vad som skulle hända
.\Compress-Images.ps1 -Source "W:\Images" -Overwrite -WhatIf

# Skriv över originalen, med säkerhetskopia
.\Compress-Images.ps1 -Source "W:\Images" -Overwrite -BackupFolder "W:\Images_backup"

# Kopiera till ny mapp i stället
.\Compress-Images.ps1 -Source "W:\Images" -Destination "W:\Images_optimized" -Quality 70

# Rapport över varje fil och vad den skulle ge, utan att ändra något
.\Compress-Images.ps1 -Source "W:\Images" -WhatIf -Overwrite -ReportPath "C:\temp\rapport.csv"
```

`-ReportPath` skriver en CSV med en rad per bild: status, nuvarande storlek, möjlig storlek och
vinst i procent, sorterad med största besparingen överst. Semikolonseparerad, så Excel öppnar den
direkt.

`-NoRecurse` tar bara bilderna som ligger rakt i källmappen och hoppar över undermapparna.
Utan den tas undermapparna alltid med.

`-FileList` pekar på en textfil med en sökväg per rad, relativt `-Source`. Bara de filerna
behandlas, vilket gör att en ny provkörning slipper gå igenom hela mappen. Kolumnen `Fil` ur en
tidigare rapport duger direkt som lista:

```powershell
Import-Csv rapport.csv -Delimiter ';' | Where-Object { $_.Status -eq 'Unsupported' } |
    ForEach-Object { $_.Fil } | Set-Content lista.txt -Encoding UTF8
```

```powershell
.\Compress-Images.ps1 -Source "C:\zpiderdata\Images" -Overwrite -NoRecurse -WhatIf -ReportPath "C:\temp\rapport.csv"
```

Utan `-BackupFolder` frågar skriptet efter en bekräftelse innan det skriver över något.

`jamo\` innehåller två engångsskript från Jamo-körningen i februari 2026, med hårdkodade
sökvägar till tre specifika filer. `Check-Images.ps1` visade varför de sprack,
`Fix-Failed-Images.ps1` komprimerade dem med CMYK-knepet. Det knepet ligger numera i
huvudflödet, så ingen av dem behövs för nytt arbete.

## PNG

Skriptet lämnar PNG orörd tills `oxipng.exe` finns bredvid det eller i PATH. Hämta verktygen en
gång på en maskin med nät, och kopiera sedan hela mappen till servern:

```powershell
.\Get-PngTools.ps1 -IncludePngQuant
```

`oxipng` ensamt är **tapsfritt** — inte en pixel ändras. På grafik och skärmbilder ger det runt
25 %, på fotografier ofta under tröskeln på 10 %, och då lämnas bilden orörd.

`-LossyPng` lägger till `pngquant`, som drar ner färgdjupet först. Det ger långt mer, och den
lämnar själv bilder orörda när färgerna inte räcker till `-PngMinQuality` (förval 65) — fotografier
faller då tillbaka på det tapsfria steget. Tre riktiga produktbilder från zpiderdata, 26 MB
tillsammans:

| Bild | Tapsfritt | `-LossyPng` |
|---|---|---|
| 50-FFB.jpg.png (9,1 MB, 3D-rendering) | under tröskeln | 95 % |
| 73896-VHF-R.png (9,8 MB, 7360x4912 foto) | under tröskeln | 72 % |
| AG-løpjakkebak.png (7,3 MB, tygstruktur) | 11 % | 68 % |

Granskade i 100 %-utsnitt: skillnaden syns inte. Transparens behålls, filnamnet ändras aldrig.
Animerad PNG går förbi `pngquant`, som annars kastar alla rutor utom den första.

```powershell
# tapsfritt, bara filerna rakt i mappen
.\Compress-Images.ps1 -Source "C:\zpiderdata\Images" -Overwrite -NoRecurse

# med pngquant, efter en torrkörning som visar vad varje bild ger
.\Compress-Images.ps1 -Source "C:\zpiderdata\Images" -Overwrite -NoRecurse -LossyPng
```

## Bildstorlek

`-MaxDimension` sätter ett tak för längsta sidan i pixlar. Bilder som redan är mindre skalas
aldrig upp, proportionerna behålls, och filnamnet ändras inte. Utan flaggan rörs aldrig
bildstorleken.

```powershell
.\Compress-Images.ps1 -Source "C:\zpiderdata\Images" -Overwrite -NoRecurse -MaxDimension 2000
```

Är bilden nedskalad gäller inte tröskeln `-MinGainPercent` — då är det måtten som är poängen,
och resultatet behålls så länge filen inte blev större. En JPEG som redan ligger på målkvaliteten
kodas ändå om när den ska skalas ned.

Nedskalningen är det som ger mest, och den kräver inga verktyg alls. `73896-VHF-R.png`,
7360x4912 och 9,6 MB, blev till 2000 px:

| | Filstorlek |
|---|---|
| bara nedskalning, inga verktyg | 809 KB |
| med `oxipng` | 701 KB |
| med `-LossyPng` | 202 KB |

## Bilder som faller ifrån

**Fel innehåll för ändelsen.** `Get-ActualFormat` läser formatet ur de första byten, och
innehållet avgör vad som händer — inte ändelsen. Hos en kund var 948 av 9293 filer felnamngivna,
varav 872 PNG med `.jpg`-ändelse på tillsammans 369 MB.

En PNG som heter `.jpg` behandlas som PNG och förblir PNG. Både webbläsare och GDI+ läser efter
innehåll, och filnamnet rörs aldrig, så ingenting bryts. BMP går samma väg, där är omkodningen
till PNG dessutom tapsfri. GIF och TIFF lämnas orörda — rutor och lager tål ingen omkodning.

AVIF, HEIC och WebP kan GDI+ inte läsa alls. De lämnas orörda, och rapportens `Anmerkning`-kolumn
säger vad filen är. Att skriva om dem till riktig JPEG går — WIC läser AVIF och HEIC när Windows
har kodeken — men det gör filen *större*, inte mindre. Det är en fix för en bild som inte visas,
inte för storleken.

**CMYK-JPEG.** Valideringen i `FromStream` avvisar dem. Skriptet läser om filen utan validering
när det händer; GDI+ klarar bilden ändå och omritningen ger ett användbart RGB-bild.

**EXIF-roterade.** Förval `-ExifRotation Skip` lämnar dem orörda, eftersom rotationen inte följer
med omkodningen. `Apply` vrider pixlarna som taggen säger, `Strip` behåller pixlarna och släpper
taggen. Båda ändrar hur bilden visas, så de är avstängda som förval.

Taggen är inte att lita på. I ett kundfall hade `937-119232.jpg` orientering 6, men pixlarna låg
redan rättvända — att följa taggen lade etiketten på sidan. Titta på bilderna innan du väljer
något annat än `Skip`.

## Tester

```
dotnet test ImageOptimizer.Tests\ImageOptimizer.Tests.csproj
```

Täcker kvalitetsuppskattningen, besluten om vad som får ändras, och överskrivning med backup mot
en riktig mappstruktur. Den norska utgåvan länkar in samma kärnfiler, så logiken finns bara på
ett ställe och testas en gång.
