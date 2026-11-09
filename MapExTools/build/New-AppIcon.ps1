<#
.SYNOPSIS
    生成 MapExTools 的程序图标 src/MapExTools/Assets/MapExTools.ico。

.DESCRIPTION
    图形沿用 Mod 游戏内 UI 的 MapExtPDX/MapExt/UI/src/assets/map-icon-box.svg（24×24 视框，
    上行 MAP、下行 EXT），底色填充、线条白色，以便在浅色与深色任务栏上都看得清。
    用 WPF 渲染为多尺寸 PNG，再打包成 PNG 编码的 .ico（Windows Vista 起支持）。
    改了图形后重新运行本脚本，并提交生成的 .ico。

    需在 STA 线程运行（WPF 要求）：
        pwsh -NoProfile -STA -File MapExTools/build/New-AppIcon.ps1
#>
[CmdletBinding()]
param(
    [string]$OutFile = (Join-Path $PSScriptRoot '../src/MapExTools/Assets/MapExTools.ico'),
    [string]$Background = '#1F5FAF'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase

if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    throw '需要 STA 线程：请用 pwsh -STA 运行本脚本'
}

# 与 map-icon-box.svg 一致的笔画：D 为路径，W 为线宽，O 为不透明度
$strokes = @(
    @{ D = 'M3.5 12 L20.5 12'; W = 1.2; O = 0.65 }                          # 中间分隔线
    @{ D = 'M4.2 10V4.5L6.8 8.5L9.4 4.5V10'; W = 1.9; O = 1 }               # M
    @{ D = 'M10.6 10L12.7 4.5L14.8 10'; W = 1.9; O = 1 }                    # A
    @{ D = 'M11.3 8.3 L14.1 8.3'; W = 1.4; O = 1 }
    @{ D = 'M16.2 10V4.5H18.3C19.4 4.5 19.8 5.3 19.8 6.5C19.8 7.7 19.4 8.5 18.3 8.5H16.2'; W = 1.9; O = 1 } # P
    @{ D = 'M8.0 14H4.4V19.5H8.0'; W = 1.9; O = 1 }                         # E
    @{ D = 'M4.4 16.75 L7.5 16.75'; W = 1.5; O = 1 }
    @{ D = 'M10.1 14L14.1 19.5'; W = 1.9; O = 1 }                           # X
    @{ D = 'M14.1 14L10.1 19.5'; W = 1.9; O = 1 }
    @{ D = 'M15.9 14H19.9'; W = 1.9; O = 1 }                                # T
    @{ D = 'M17.9 14V19.5'; W = 1.9; O = 1 }
)

function New-IconPng([int]$Size) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $dc = $visual.RenderOpen()
    $scale = $Size / 24.0
    $dc.PushTransform([Windows.Media.ScaleTransform]::new($scale, $scale))

    # 外层圆角框：填充底色 + 白色描边（svg 中 rect x=2 y=2 w=20 h=20 rx=3.5）
    $bg = [Windows.Media.SolidColorBrush]([Windows.Media.ColorConverter]::ConvertFromString($Background))
    $white = [Windows.Media.Brushes]::White
    $framePen = [Windows.Media.Pen]::new($white, 1.8)
    $dc.DrawRoundedRectangle($bg, $framePen, [Windows.Rect]::new(2, 2, 20, 20), 3.5, 3.5)

    foreach ($s in $strokes) {
        $brush = $white.Clone()
        $brush.Opacity = $s.O
        $pen = [Windows.Media.Pen]::new($brush, $s.W)
        $pen.StartLineCap = 'Round'
        $pen.EndLineCap = 'Round'
        $pen.LineJoin = 'Round'
        $dc.DrawGeometry($null, $pen, [Windows.Media.Geometry]::Parse($s.D))
    }

    $dc.Pop()
    $dc.Close()

    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($Size, $Size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $ms = [IO.MemoryStream]::new()
    $encoder.Save($ms)
    , $ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = foreach ($size in $sizes) { , (New-IconPng $size) }

# ICO：6 字节文件头 + 每个尺寸 16 字节目录项 + PNG 数据
$out = [IO.MemoryStream]::new()
$w = [IO.BinaryWriter]::new($out)
$w.Write([uint16]0)              # 保留
$w.Write([uint16]1)              # 类型：图标
$w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim)         # 宽（256 记为 0）
    $w.Write([byte]$dim)         # 高
    $w.Write([byte]0)            # 调色板颜色数
    $w.Write([byte]0)            # 保留
    $w.Write([uint16]1)          # 色彩平面
    $w.Write([uint16]32)         # 位深
    $w.Write([uint32]$images[$i].Length)
    $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush()

$target = [IO.Path]::GetFullPath($OutFile)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
[IO.File]::WriteAllBytes($target, $out.ToArray())
Write-Host ("已生成 {0}（{1} 个尺寸，{2:N0} 字节）" -f $target, $sizes.Count, $out.Length)
