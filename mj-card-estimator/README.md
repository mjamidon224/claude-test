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
| `C` | Switch between Lines and 4 corners |
| `U` | Upload an image |

1. Paste the screenshot. The scan takes well under a second.
2. Check the guides. Pink lines should sit on the card edge, and blue lines where the border meets the frame. Drag any line that's off. If the photo was taken at an angle, switch to **4 corners** (below).
3. Pick the card. If you open the page on claude.ai, Claude names it for you, reads a slab label if there is one, and fills in the current bid.
4. Type the current bid. The verdict tells you how much room you have, or how far over your max it is.

Switch to **Already graded** for slabbed cards. A qualifier (OC, ST, PD and so on) values the card about two grades lower.

## Crooked cards and photos taken at an angle

When a card sits rotated in the photo, or the photo was taken at an angle, border widths measured straight across come out wrong. On paste, the scan finds the card's four corners. If the card is more than 1° crooked or its edges aren't parallel, it flattens the card before measuring, so the guides land square on its edges with nothing to drag. This still works when the card nearly fills the photo or a corner is cut off by the photo's edge. **Undo flatten** shows the original photo.

To place the corners yourself, switch the guides to **4 corners** and you get draggable dots instead of lines:

- **Pink dots** go on the card's four corners, in any shape. When you let go, the app solves the perspective transform from those corners and warps the card flat to a true 2.5 x 3.5 rectangle. Corners, edges and centering are then measured on the flattened card. The view extends past the photo (its edge is dashed), so a dot can reach a corner the photo cut off.
- **Blue dots** mark the inner border. They stay parallel to the card's edges, following the same perspective. Drag one if the scan missed the border. Centering updates as you drag. Where a blue dot sits under a pink one, pressing on the pink dot moves the pink one.

A magnifier shows the area under the dot while you drag. **Undo flatten** goes back to the original photo. Switching back to Lines after flattening shows the flattened card with the usual straight guides.

## How the grade estimate works

The scan finds the card in the screenshot, fits a straight line to each edge, and measures:

- **Centering**: border width on each side. PSA's front limits set a hard cap: 55/45 for a 10, 60/40 for a 9, 65/35 for an 8, 70/30 for a 7, 80/20 for a 6, 85/15 for a 5 or 4, and 90/10 for a 3.
- **Corners**: how much of a sharp square corner is missing, expressed as an equivalent rounding radius.
- **Edges**: dents in the fitted edge line, roughness, and whitening where the border is dark.
- **Print and surface**: specks on the border, glare and sharpness. This is the weakest signal from a screenshot, and it's where the Claude check helps most.

PSA grades by the weakest point, so the overall grade leans on the lowest subgrade and is capped by centering. Each measurement carries an uncertainty that depends on image size and sharpness. The page samples 4,000 possible outcomes to get a grade distribution, then prices each outcome. "Conservative" is the 25th percentile of those prices.

## Price data

Prices come from the [SportsCardsPro](https://www.sportscardspro.com/) price guide (October 2026 snapshot) and are built into the page, so it never goes online to price a card. For each card it stores Ungraded, Grade 1 through 9, PSA 10 and BGS/SGC 9.5. Some cards are missing grades on SportsCardsPro. Those grades are estimated from the grades next to them, using ratios from cards that list every grade, and show an **est.** tag. The 1984-85 Star Court Kings isn't on SportsCardsPro, so its prices are rough estimates and are labeled that way.

To refresh a card's prices, open it on SportsCardsPro, copy the Full Price Guide table, and paste it into **Price data for this card → Update from SportsCardsPro**. The page reads the prices out of the pasted text and saves them on your device. Nothing is sent anywhere. You can also type any price by hand. **Other Jordan card** works the same way for cards not on the list.

Thin markets such as 1984-85 Star and the refractors are marked Low confidence. SportsCardsPro's low grades for those cards are uneven because few sell.

## Limits

- This estimates a grade. PSA's actual grade can differ.
- A front screenshot can't show the back, gloss, light scratches, trimming or alterations.
- Raw 1986 Fleer and Star Jordans are heavily counterfeited. Buy those slabbed.
- The PSA grading fee is a rough tier estimate by declared value. Type the real fee if you know it.
