# Jordan Card Appraiser

A single-page tool for bidding on classic Michael Jordan cards. Paste a listing screenshot and it estimates a PSA grade, what the card is worth at that grade, and the most you should bid after buyer's premium, tax, shipping, grading fees and your margin.

Open `index.html` in a browser. Everything runs locally in the page with no server and no build step.

## Windows app

`JordanCardAppraiser.exe` is the same page in its own window. It's a single 2.7 MB file with nothing to install. It uses the Microsoft Edge WebView2 runtime that comes with Windows 10 and 11; on a PC without WebView2 it opens the page in your browser instead. Saved prices and settings live in `%LOCALAPPDATA%\JordanCardAppraiser`.

The exe isn't code-signed, so the first launch shows "Windows protected your PC". Click **More info**, then **Run anyway**.

To build it, install Go 1.24 or later and run `./build-exe.sh`. Set `GOARCH=arm64` for Windows on Arm. The output goes to `dist/`. The build embeds `index.html`, so rebuild after you change the page.

The Claude check only works when the page is opened on claude.ai. In the exe, pick the card yourself.

## Using it

| Key | Action |
| --- | --- |
| `Ctrl`/`⌘` + `V` | Paste a screenshot (or drag and drop an image) |
| `/` | Search for the card (`86 fleer`, `star 101`, `finest refractor`) |
| `A` | Ask Claude to check the screenshot (claude.ai only) |
| `U` | Upload an image |

1. Paste the screenshot. The scan takes well under a second.
2. Check the guides. Pink lines should sit on the card edge, and blue lines where the border meets the frame. Drag any line that's off.
3. Pick the card. If you open the page on claude.ai, Claude names it for you, reads a slab label if there is one, and fills in the current bid.
4. Type the current bid. The verdict tells you how much room you have, or how far over your max it is.

Switch to **Already graded** for slabbed cards. A qualifier (OC, ST, PD and so on) values the card about two grades lower.

## How the grade estimate works

The scan finds the card in the screenshot, fits a straight line to each edge, and measures:

- **Centering**: border width on each side. PSA's front limits set a hard cap: 55/45 for a 10, 60/40 for a 9, 65/35 for an 8, 70/30 for a 7, 80/20 for a 6, 85/15 for a 5 or 4, and 90/10 for a 3.
- **Corners**: how much of a sharp square corner is missing, expressed as an equivalent rounding radius.
- **Edges**: dents in the fitted edge line, roughness, and whitening where the border is dark.
- **Print and surface**: specks on the border, glare and sharpness. This is the weakest signal from a screenshot, and it's where the Claude check helps most.

PSA grades by the weakest point, so the overall grade leans on the lowest subgrade and is capped by centering. Each measurement carries an uncertainty that depends on image size and sharpness. The page samples 4,000 possible outcomes to get a grade distribution, then prices each outcome. "Conservative" is the 25th percentile of those prices.

## Price data

Built-in prices are approximate comps as of October 2026, drawn from public sales (Goldin, Heritage, Card Ladder, PSA auction prices and price indexes). Each card shows its sources and a confidence level. Thin markets such as 1984-85 Star are marked Low. Edit any price in **Price data for this card**; your edits are saved in your browser. **Other Jordan card** takes your own comps for anything not on the list.

## Limits

- This estimates a grade. PSA's actual grade can differ.
- A front screenshot can't show the back, gloss, light scratches, trimming or alterations.
- Raw 1986 Fleer and Star Jordans are heavily counterfeited. Buy those slabbed.
- The PSA grading fee is a rough tier estimate by declared value. Type the real fee if you know it.
