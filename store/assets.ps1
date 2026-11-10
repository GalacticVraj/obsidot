param([string]$Dir)
# Store logos for Obsidot, drawn at every size Windows asks for: black glass, a faint dot grid,
# a ring of lit dots (the O) and the red second on the rim. Same artwork as src\obsidot.ico.
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $Dir | Out-Null

function Draw([int]$n) {
    $b = New-Object System.Drawing.Bitmap $n, $n, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($b); $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
    $c = $n / 2.0; $rad = $n * 0.47
    $rect = New-Object System.Drawing.RectangleF ([float]($c - $rad)), ([float]($c - $rad)), ([float]($rad * 2)), ([float]($rad * 2))
    $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.Color]::FromArgb(255, 44, 44, 48), [System.Drawing.Color]::FromArgb(255, 10, 10, 11), [single]90)
    $g.FillEllipse($lg, $rect); $lg.Dispose()
    $grid = $n -ge 48; $pitch = $rad / 4.6; $ds = $pitch * 0.62
    for ($row = -4; $row -le 4; $row++) { for ($q = -4; $q -le 4; $q++) {
        $d = [math]::Sqrt($q * $q + $row * $row); if ($d -gt 4.2) { continue }
        $lit = [math]::Abs($d - 2.7) -lt 0.62
        if (-not $lit -and -not $grid) { continue }
        $a = if ($lit) { 245 } else { 26 }; $s = if ($lit) { $ds * 1.18 } else { $ds }
        $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, 255, 255, 255))
        $g.FillEllipse($br, [float]($c + $q * $pitch - $s / 2), [float]($c + $row * $pitch - $s / 2), [float]$s, [float]$s); $br.Dispose()
    } }
    $ang = -50 * [math]::PI / 180; $rd = $rad * 0.86; $s = [math]::Max(2.2, $n * 0.085)
    $red = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 232, 36, 32))
    $g.FillEllipse($red, [float]($c + [math]::Cos($ang) * $rd - $s / 2), [float]($c + [math]::Sin($ang) * $rd - $s / 2), [float]$s, [float]$s); $red.Dispose()
    if ($n -ge 32) { $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(110, 255, 255, 255)), ([float]([math]::Max(1, $n / 160))); $g.DrawArc($pen, [float]($c - $rad + $n * 0.01), [float]($c - $rad + $n * 0.01), [float]($rad * 2 - $n * 0.02), [float]($rad * 2 - $n * 0.02), 205, 130); $pen.Dispose() }
    $g.Dispose(); return $b
}

# the logo, `fill` of the smaller side, centred on a transparent w x h canvas
function Save([string]$file, [int]$w, [int]$h, [double]$fill) {
    $n = [int][math]::Round([math]::Min($w, $h) * $fill)
    $icon = Draw $n
    $b = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($b); $g.DrawImageUnscaled($icon, [int](($w - $n) / 2), [int](($h - $n) / 2)); $g.Dispose(); $icon.Dispose()
    $b.Save((Join-Path $Dir $file), [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
}

foreach ($sc in 100, 200, 400) {
    $k = $sc / 100
    Save "StoreLogo.scale-$sc.png" (50 * $k) (50 * $k) 1.0
    Save "Square44x44Logo.scale-$sc.png" (44 * $k) (44 * $k) 1.0
    Save "Square150x150Logo.scale-$sc.png" (150 * $k) (150 * $k) 0.66
    if ($sc -le 200) { Save "Wide310x150Logo.scale-$sc.png" (310 * $k) (150 * $k) 0.66 }
}
foreach ($t in 16, 24, 32, 48, 256) {   # taskbar and Start list: full-bleed, no plate
    Save "Square44x44Logo.targetsize-$t.png" $t $t 1.0
    Save "Square44x44Logo.targetsize-${t}_altform-unplated.png" $t $t 1.0
    Save "Square44x44Logo.targetsize-${t}_altform-lightunplated.png" $t $t 1.0
}
