$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'assets'
$extension = Join-Path $root 'browser-extension/icons'
New-Item -ItemType Directory -Force $assets,$extension | Out-Null
$frames = @()
foreach ($size in @(16,24,32,48,64,128,256)) {
    $bitmap = [System.Drawing.Bitmap]::new($size,$size)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = 'AntiAlias'
    $g.ScaleTransform($size / 256.0,$size / 256.0)
    $shape = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $shape.AddArc(8,8,88,88,180,90); $shape.AddArc(160,8,88,88,270,90)
    $shape.AddArc(160,160,88,88,0,90); $shape.AddArc(8,160,88,88,90,90); $shape.CloseFigure()
    $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Point]::new(0,0),[System.Drawing.Point]::new(256,256),[System.Drawing.ColorTranslator]::FromHtml('#A2F3D5'),[System.Drawing.ColorTranslator]::FromHtml('#52BE9A'))
    $g.FillPath($brush,$shape)
    $pen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#102A20'),18)
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $g.DrawLine($pen,128,60,128,156)
    $g.DrawLines($pen,[System.Drawing.PointF[]]@([System.Drawing.PointF]::new(91,122),[System.Drawing.PointF]::new(128,159),[System.Drawing.PointF]::new(165,122)))
    $g.DrawLines($pen,[System.Drawing.PointF[]]@([System.Drawing.PointF]::new(72,174),[System.Drawing.PointF]::new(72,195),[System.Drawing.PointF]::new(184,195),[System.Drawing.PointF]::new(184,174)))
    $memory = [System.IO.MemoryStream]::new(); $bitmap.Save($memory,[System.Drawing.Imaging.ImageFormat]::Png)
    $frames += @{ Size=$size; Bytes=$memory.ToArray() }
    [System.IO.File]::WriteAllBytes((Join-Path $assets "icon-$size.png"),$memory.ToArray())
    if ($size -in @(16,32,48,128)) { [System.IO.File]::WriteAllBytes((Join-Path $extension "icon-$size.png"),$memory.ToArray()) }
    $memory.Dispose(); $pen.Dispose(); $brush.Dispose(); $shape.Dispose(); $g.Dispose(); $bitmap.Dispose()
}
$stream = [System.IO.File]::Create((Join-Path $assets 'OpenDownload.ico'))
$writer = [System.IO.BinaryWriter]::new($stream)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($frame in $frames) {
    $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
    $offset += $frame.Bytes.Length
}
foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
$writer.Dispose()
