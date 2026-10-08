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
| EXIF-rotation | Rotationen följer inte med omkodningen, och bilden skulle visas vriden. |
| Format som inte kan komprimeras säkert | BMP, GIF och TIFF. System.Drawing kan inte göra dem mindre utan att riskera kvaliteten. |

Filnamn och filändelse ändras aldrig, så referenser från html-sidor fortsätter peka rätt.
Genomskinlighet i PNG plattas inte mot vit botten.

PNG optimeras i praktiken aldrig — System.Drawing saknar styrning av PNG-komprimeringen, så
omkodningen blir lika stor eller större och bilden lämnas orörd. Verklig PNG-vinst kräver
`oxipng` eller `pngquant`.

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

Utan `-BackupFolder` frågar skriptet efter en bekräftelse innan det skriver över något.

`jamo\` innehåller två engångsskript från Jamo-körningen i februari 2026, med hårdkodade
sökvägar till tre specifika filer. `Check-Images.ps1` visade varför de sprack,
`Fix-Failed-Images.ps1` komprimerade dem med CMYK-knepet. Det knepet ligger numera i
huvudflödet, så ingen av dem behövs för nytt arbete.

## Tester

```
dotnet test ImageOptimizer.Tests\ImageOptimizer.Tests.csproj
```

Täcker kvalitetsuppskattningen, besluten om vad som får ändras, och överskrivning med backup mot
en riktig mappstruktur. Den norska utgåvan länkar in samma kärnfiler, så logiken finns bara på
ett ställe och testas en gång.
