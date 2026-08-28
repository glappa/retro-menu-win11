# retro-menu-win11

Ein Startmenü im Windows-XP-Stil für Windows 11 – das Gegenstück zu
[RetroBar](https://github.com/dremin/RetroBar), das dasselbe mit der Taskleiste macht.

RetroBar ersetzt die Taskleiste, lässt aber das moderne Windows-11-Startmenü stehen.
Genau diese Lücke schließt dieses Projekt: Die Windows-Taste (und der Start-Knopf von
RetroBar) öffnen ab jetzt ein klassisches zweispaltiges Menü statt der Kacheloberfläche.

![Das Menü](docs/screenshot.png)

## Was drin ist

* **Nachgemessenes XP-Layout** statt Nachempfinden: 384 Pixel breit, zwei Spalten zu
  je 190, Kopf 54 Pixel, Fußzeile 36, Zeilen 34 bzw. 26 Pixel hoch, Tahoma 11 – dazu
  die dreizehn Farbstufen des Kopf-Verlaufs, die orange Haarlinie darunter, der
  blaue 2-Pixel-Rahmen und die flache Auswahlfarbe `#2F71CD`. Die Werte sind an
  einer originalgetreuen XP-Nachbildung Pixel für Pixel abgenommen, nicht geschätzt.
* **Die beiden XP-Sonderplätze oben links**: „Internet" und „E-Mail", fett, mit dem
  Namen des tatsächlichen Standardprogramms als graue zweite Zeile darunter.
* **Rechte Spalte in XP-Reihenfolge**: Eigene Dateien, Zuletzt verwendete Dokumente ▸,
  Eigene Bilder, Eigene Musik, Arbeitsplatz — Systemsteuerung, Programmzugriff,
  Verbindung herstellen ▸, Drucker und Faxgeräte — Hilfe, Suchen, Ausführen…
  Die ersten fünf fett, wie damals; die beiden Pfeil-Einträge klappen bei Hover auf.
* **Auto-Hide-Taskleiste fährt mit hoch**, solange das Menü offen ist — genau wie
  unter XP.
* **„Alle Programme"** als aufklappendes Kaskadenmenü, direkt aus den beiden
  Startmenü-Ordnern (System + Benutzer) zusammengeführt, mit echten Programmsymbolen.
* **Store-Apps** werden mitgelistet – die haben keine Verknüpfung auf der Platte und
  tauchen in den klassischen Ordnern deshalb nie auf.
* **Suchfeld** über alle gefundenen Programme, Enter startet den ersten Treffer.
  Standardmäßig aus, weil XP keines hatte — einschaltbar in den Einstellungen.
* **Anheften und Verlauf**: Rechtsklick auf einen Eintrag zum Anheften, Lösen, als
  Administrator starten oder Dateipfad öffnen. Die Liste „häufig verwendet" füllt sich
  selbst, genau wie früher.
* **Themes**, die zu RetroBar passen – und auf Wunsch automatisch dessen Design
  übernehmen.
* **Ein Startknopf, zwei Wege**: Windows-Taste oder der Start-Knopf von RetroBar.

![Alle Programme](docs/all-programs.png)

## Installation

Gebraucht wird das **.NET 8 Desktop Runtime** (oder das SDK zum Selberbauen).

```bash
git clone https://github.com/glappa/retro-menu-win11.git
```

Bauen und starten:

```bash
dotnet build src/RetroMenu/RetroMenu.csproj -c Release
```

Eine einzelne EXE zum Weitergeben:

```bash
powershell -ExecutionPolicy Bypass -File publish.ps1
```

Das Ergebnis liegt danach in `publish/RetroMenu.exe`. Zum automatischen Start bei
jeder Anmeldung genügt der Haken **„Mit Windows starten"** in den Einstellungen.

## Bedienung

| Aktion | Wirkung |
| --- | --- |
| Windows-Taste | Menü auf/zu |
| Start-Knopf in RetroBar | Menü auf/zu |
| Klick auf das Tray-Symbol | Menü auf |
| Rechtsklick aufs Tray-Symbol | Einstellungen, Programmliste neu einlesen, Beenden |
| Esc | Menü zu |
| Tippen | sucht in allen Programmen |
| Rechtsklick auf einen Eintrag | Anheften, Lösen, als Administrator starten, Dateipfad |
| Zeigen auf ▸-Einträge | klappt „Zuletzt verwendete Dokumente" bzw. „Verbindung herstellen" auf |

Alle Kombinationen mit der Windows-Taste (Win+E, Win+R, Win+D, Win+L …) funktionieren
unverändert weiter.

## Wie das Abfangen der Windows-Taste funktioniert

Windows öffnet sein Startmenü beim **Loslassen** der Windows-Taste – aber nur, wenn
zwischendurch keine andere Taste gedrückt wurde. Das Programm hängt sich mit einem
`WH_KEYBOARD_LL`-Hook in den Tastaturstrom und schiebt bei einem einzelnen Tippen kurz
vor dem Loslassen eine unbelegte Taste ein. Für Windows sieht das wie eine
Tastenkombination aus, sein eigenes Menü bleibt zu – und wir zeigen unseres.

Weil ein solcher Hook auch simulierte Eingaben sieht, funktioniert der Start-Knopf von
RetroBar ohne jede Anpassung mit: RetroBar ruft dafür `ShellHelper.ShowStartMenu()` aus
ManagedShell auf, und das simuliert genau so einen einzelnen Windows-Tastendruck.

Falls das auf einem Rechner nicht greift, gibt es in den Einstellungen zwei Alternativen:

| Modus | Verhalten |
| --- | --- |
| **Abfangen** (Standard) | Windows-Taste läuft durch, wird nur neutralisiert |
| **Vollständig schlucken** | Taste wird abgefangen und nur bei echten Kombinationen wieder eingespeist |
| **Nicht anfassen** | Hook aus; das Menü geht dann nur über Tray-Symbol und RetroBar |

## Die Taskleiste kommt mit hoch

Läuft RetroBar mit automatischem Ausblenden, fährt die Leiste beim Öffnen des Menüs
wieder heraus und bleibt stehen, bis das Menü zugeht.

Erreicht wird das ohne einen einzigen Eingriff in RetroBar: RetroBar sucht zehnmal
pro Sekunde nach einem offenen Startmenü und erkennt dabei neben dem modernen Menü
auch fremde — unter anderem an der Fensterklasse `OpenShell.CMenuContainer`. Solange
unser Menü offen ist, halten wir ein leeres, vollständig durchsichtiges und
klickdurchlässiges Fenster dieser Klasse über der Menüfläche. RetroBar sieht ein
Startmenü, lässt die Leiste oben und setzt sie bei mehreren Bildschirmen sogar auf
den richtigen. Abschaltbar über **„Taskleiste beim Öffnen einblenden"**.

## Themes

| Theme hier | RetroBar-Designs, die darauf abgebildet werden |
| --- | --- |
| Windows XP Blue | Windows XP Blue, XP Embedded Style, Vista Basic, System XP/Vista |
| Windows XP Olive Green | Windows XP Olive Green |
| Windows XP Silver | Windows XP Silver |
| Windows XP Royale | XP Royale, Royale Noir, Zune Style, Watercolor, Longhorn/Vista Aero |
| Classic Grey | Windows 95-98, Windows 2000, Windows Me, XP Classic, Vista Classic, System |

Ist **„Design von RetroBar übernehmen"** aktiv (Standard), liest das Menü RetroBars
`settings.json` mit und wechselt das Design automatisch mit, sobald dort eines
umgestellt wird. Geschrieben wird in RetroBars Dateien nie.

Übernommen wird von dort außerdem die Sprache, die Kantenglättung der Schrift und –
einmalig beim ersten Start – die Quick-Launch-Reihenfolge als Startbelegung der
angehefteten Programme.

## Einstellungen

Alles liegt in `%AppData%\RetroMenuWin11\settings.json`:

| Schlüssel | Bedeutung |
| --- | --- |
| `Theme`, `FollowRetroBarTheme` | Design, bzw. RetroBar folgen |
| `Language` | `auto`, `de` oder `en` |
| `WinKeyMode` | `Neutralize`, `Swallow` oder `Off` |
| `FrequentCount` | Wie viele „häufig verwendet"-Einträge |
| `MenuScale` | 1.0 ist XP-Originalgröße; auf großen Bildschirmen darf es mehr sein |
| `KeepTaskbarVisible` | Auto-Hide-Taskleiste einblenden, solange das Menü offen ist |
| `ShowSearchBox`, `ShowStoreApps`, `ShowRunAsAdmin` | Ein/aus |
| `Pinned`, `LaunchCounts` | Angeheftetes und Startzähler |
| `UserName` | Überschreibt den angezeigten Namen |

Daneben liegt `retromenu.log`. Mit gesetzter Umgebungsvariable `RETROMENU_DEBUG=1`
protokolliert das Programm zusätzlich jedes Ereignis der Windows-Taste – hilfreich,
wenn der Hook auf einem Rechner nicht so will.

## Bekannte Grenzen

* Über Fenstern, die **als Administrator** laufen, sieht ein normaler Tastatur-Hook
  nichts. Wer das Menü auch dort per Windows-Taste braucht, muss RetroMenu selbst
  erhöht starten.
* Windows 11 liefert für „Arbeitsplatz" über jede Icon-API nur ein
  Standard-Ordnersymbol. Solche Symbole holt das Menü deshalb direkt aus
  `imageres.dll`.
* Die Kaskade zeigt die Startmenü-Ordner so, wie sie auf der Platte liegen – Programme
  ohne Verknüpfung erscheinen nur unter „Store-Apps" und in der Suche.
* Die Symbole sind die von Windows 11. Ein Menü in XP-Maßen mit modernen Symbolen ist
  der ehrlichste Kompromiss: XPs eigene Icons gehören Microsoft und liegen deshalb
  nicht in diesem Repository.
* Das Menü ist so breit wie unter XP, also 384 Pixel. Auf einem 4K-Bildschirm wirkt
  das klein – dafür gibt es `MenuScale` bzw. **Menügröße** in den Einstellungen.
* Mehrere Monitore werden unterstützt; das Menü erscheint an der Leiste des Monitors,
  auf dem der Mauszeiger steht.

## Fahrplan

* Ein echtes 9x-Layout (einspaltig, mit senkrechtem Banner) für das graue Theme
* Scrollleisten im XP-Stil statt der WPF-Standardleisten
* Nachgemessene Verläufe auch für Olive und Silber – die beiden entstehen bisher aus
  den blauen Werten per Farbtonverschiebung
* Tastaturnavigation in den Spalten (aktuell nur Suche und Kaskade)
* Weitere Sprachen

## Danke

An [dremin](https://github.com/dremin) für RetroBar und ManagedShell – ohne die
Vorarbeit gäbe es hier nichts anzuschließen.

## Lizenz

MIT, siehe [LICENSE](LICENSE).

---

### English, briefly

An XP-style Start menu for Windows 11, built as the companion piece to
[RetroBar](https://github.com/dremin/RetroBar): RetroBar replaces the taskbar, this
replaces the Start menu it leaves behind. Sizes, gradients and colours are measured
off the original panel rather than approximated: 384 pixels wide, two 190-pixel
columns, a 54-pixel header over Luna's orange hairline, 11-pixel Tahoma and the flat
`#2F71CD` selection. A low-level keyboard hook turns a lone Windows-key press into the
menu while every Win+X shortcut keeps working, and because such a hook also sees
simulated input, RetroBar's own Start button opens it too without any patching. An
auto-hidden RetroBar taskbar rises while the menu is open, the way XP behaved.
Themes mirror RetroBar's and can follow its setting automatically. Requires the
.NET 8 Desktop Runtime; build with
`dotnet build src/RetroMenu/RetroMenu.csproj -c Release`.
