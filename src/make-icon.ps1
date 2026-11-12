# Builds assets\logo.png (rounded tile) and src\app.ico (multi-size, PNG-compressed)
# from the original RigCord logo in assets\logo-original.jpg.
$root = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing
$src = [System.Drawing.Bitmap]::FromFile("$root\assets\logo-original.jpg")
$cx = 510; $cy = 512   # centre of the hexagon in the original image

function Round-Path([float]$x, [float]$y, [float]$w, [float]$h, [float]$rad) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $rad * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); return $p
}

# Renders the logo as a rounded tile. $half = how much of the original around the centre to keep
function Render([int]$size, [int]$half) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
    $tex = New-Object System.Drawing.TextureBrush $src
    $scale = $size / (2.0 * $half)
    $tex.ScaleTransform($scale, $scale)
    $tex.TranslateTransform(-($cx - $half), -($cy - $half))
    $g.FillPath($tex, (Round-Path 0 0 $size $size ($size * 0.22)))
    $g.Dispose(); return $bmp
}

$logo = Render 512 430
$logo.Save("$root\assets\logo.png", [System.Drawing.Imaging.ImageFormat]::Png)
$logo.Dispose()

$sizes = 16, 20, 24, 32, 48, 64, 128, 256
$pngs = @()
foreach ($s in $sizes) {
    $half = if ($s -le 32) { 380 } else { 430 }   # tighter crop for tiny icons
    $b = Render $s $half
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
    $pngs += , $ms.ToArray()
}
$fs = [System.IO.File]::Create("$PSScriptRoot\app.ico")
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]$pngs[$i].Length); $w.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close()
$src.Dispose()
Write-Host "Logo + icon written"
