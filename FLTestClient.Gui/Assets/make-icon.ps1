# Renders Assets\Logo.xaml (DrawingImage "LogoMark") into a multi-size app icon (PNG-compressed ICO)
# and an optional preview PNG. Run with Windows PowerShell (STA), e.g.:
#   powershell -ExecutionPolicy Bypass -File make-icon.ps1 [-Preview preview.png]
param([string]$Preview = "")

Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$dict = [System.Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText((Join-Path $here "Logo.xaml")))
$logo = $dict["LogoMark"]

function Render([int]$size) {
    $image = New-Object System.Windows.Controls.Image
    $image.Source = $logo
    $image.Width = $size; $image.Height = $size
    [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($image, "HighQuality")
    $image.Measure((New-Object System.Windows.Size $size, $size))
    $image.Arrange((New-Object System.Windows.Rect 0, 0, $size, $size))
    $bmp = New-Object System.Windows.Media.Imaging.RenderTargetBitmap $size, $size, 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
    $bmp.Render($image)
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bmp))
    $ms = New-Object IO.MemoryStream
    $enc.Save($ms)
    return ,$ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($s in $sizes) { ,(Render $s) }

# ICO: ICONDIR, one ICONDIRENTRY per size, then the PNG data.
$out = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $images[$i].Length
    $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$len); $w.Write([uint32]$offset)
    $offset += $len
}
foreach ($img in $images) { $w.Write($img) }
[IO.File]::WriteAllBytes((Join-Path $here "FLTestClient.ico"), $out.ToArray())
"FLTestClient.ico: $($sizes -join ', ') px"

if ($Preview) {
    [IO.File]::WriteAllBytes($Preview, (Render 512))
    "Preview: $Preview"
}
