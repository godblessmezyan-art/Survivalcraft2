[CmdletBinding()]
param([string]$GameDir = 'D:\SC', [string]$ModDll, [string]$PreviewPath)
$ErrorActionPreference = 'Stop'
if (-not $ModDll) { $ModDll = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\artifacts\build\SCIEPort.dll' }
[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Engine.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'EntitySystem.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Survivalcraft.exe'))
$mod = [Reflection.Assembly]::LoadFrom((Resolve-Path $ModDll))
# Use the audited game's own decoder and actual Pericles metrics, with no GPU texture.
# No proprietary resource is copied into the repository or the mod package.
Add-Type -AssemblyName System.IO.Compression
$gameAssembly=[Game.CanvasWidget].Assembly
$decoder=$gameAssembly.GetType('Game.SurvivalCraftModEntity').GetMethod('GetDecipherStream',[Reflection.BindingFlags]'NonPublic,Static')
$source=[IO.File]::OpenRead((Join-Path $GameDir 'Content.scpak'))
try { $decoded=$decoder.Invoke($null,@($source)) } finally { $source.Dispose() }
$zip=New-Object IO.Compression.ZipArchive($decoded,[IO.Compression.ZipArchiveMode]::Read)
if ($PreviewPath) {
    Add-Type -AssemblyName System.Drawing
    $atlasStream=$zip.GetEntry('Assets/Fonts/Pericles.png').Open()
    $atlasSource=[Drawing.Bitmap]::FromStream($atlasStream)
    $atlas=New-Object Drawing.Bitmap($atlasSource)
    $atlasSource.Dispose(); $atlasStream.Dispose()
}
$reader=New-Object IO.StreamReader($zip.GetEntry('Assets/Fonts/Pericles.lst').Open())
try {
    $glyphs=New-Object 'System.Collections.Generic.List[Engine.Media.BitmapFont+Glyph]'
    $glyphCount=[int]$reader.ReadLine()
    for ($i=0;$i -lt $glyphCount;$i++) {
        $parts=$reader.ReadLine().Split([char[]]" `t",[StringSplitOptions]::None)
        if ($parts.Length -eq 9) { $parts=@(' ')+$parts[2..8] }
        $numbers=@($parts[1..7] | ForEach-Object { [single]::Parse($_,[Globalization.CultureInfo]::InvariantCulture) })
        $glyph=New-Object Engine.Media.BitmapFont+Glyph([char]$parts[0],(New-Object Engine.Vector2($numbers[0],$numbers[1])),(New-Object Engine.Vector2($numbers[2],$numbers[3])),(New-Object Engine.Vector2($numbers[4],$numbers[5])),$numbers[6])
        $glyphs.Add($glyph)
    }
    $fontHeight=[single]::Parse($reader.ReadLine(),[Globalization.CultureInfo]::InvariantCulture)
    $spacing=$reader.ReadLine().Split([char[]]" `t",[StringSplitOptions]::RemoveEmptyEntries)
    $fontScale=[single]::Parse($reader.ReadLine(),[Globalization.CultureInfo]::InvariantCulture)
    $fallback=[char]$reader.ReadLine()
    $font=New-Object Engine.Media.BitmapFont($null,$glyphs,$fallback,$fontHeight,(New-Object Engine.Vector2([single]$spacing[0],[single]$spacing[1])),$fontScale)
} finally { $reader.Dispose(); $zip.Dispose(); $decoded.Dispose() }
$caches = [Game.ContentManager].GetField('Caches', [Reflection.BindingFlags]'NonPublic,Static').GetValue($null)
$fonts = New-Object 'System.Collections.Generic.List[object]'; $fonts.Add($font)
$caches['Fonts/Pericles'] = $fonts
[Game.LanguageControl]::KeyWords['Usual'] = New-Object SimpleJson.JsonObject
$script:count = 0
function Check($condition, $message) { $script:count++; if (-not $condition) { throw $message } }
function Field($object, $name) { $object.GetType().GetField($name,[Reflection.BindingFlags]'NonPublic,Instance').GetValue($object) }
function Finger($id, $state, $widget, $x, $y) {
    $touch = New-Object Engine.Input.TouchLocation
    $touch.Id=$id; $touch.State=[Engine.Input.TouchLocationState]$state
    $touch.Position=$widget.WidgetToScreen((New-Object Engine.Vector2($x,$y)))
    [Engine.Input.Touch]::m_touchLocations.Add($touch)
}
function Frame { $lever.Update(); $toggle.Update() }
$hud=[Activator]::CreateInstance($mod.GetType('SCIEPort.Features.AirVehicles.Hud.HelicopterFlightHud'),$true)
$root=New-Object Game.CanvasWidget
$root.IsHitTestVisible=$false
$root.WidgetsHierarchyInput=New-Object Game.WidgetInput([Game.WidgetInputDevice]::Touch)
Check ($hud.Attach($root)) 'HUD attachment failed'
$lever=Field $hud 'm_lever'; $toggle=Field $hud 'm_hover'
$panel=Field $hud 'm_panel'; $instruments=Field $hud 'm_instruments'; $grip=Field $hud 'm_grip'
$mode=[Enum]::Parse($mod.GetType('SCIEPort.Features.AirVehicles.HelicopterControlMode'),'Grounded')
foreach ($size in @(@(1000,600),@(812,375),@(1280,720),@(640,480))) {
    $root.Size=New-Object Engine.Vector2($size[0],$size[1])
    for ($i=0;$i -lt 3;$i++) {
        [Game.Widget]::LayoutWidgetsHierarchy($root,$root.Size)
        $hud.Refresh(0.02,0,0,70,[Engine.Vector3]::Zero,$true,$mode,1)
    }
    [Game.Widget]::LayoutWidgetsHierarchy($root,$root.Size)
    foreach ($label in @($root.AllChildren | Where-Object { $_ -is [Game.LabelWidget] })) {
        $measured=$font.MeasureText($label.Text,(New-Object Engine.Vector2($label.FontScale,$label.FontScale)),[Engine.Vector2]::Zero)
        Check ($measured.X -le $label.ActualSize.X -and $measured.Y -le $label.ActualSize.Y) ("Cockpit text clipped: " + $label.Text)
    }
    $corner=$panel.WidgetToScreen([Engine.Vector2]::Zero)
    $end=$panel.WidgetToScreen($panel.ActualSize)
    Check ($corner.X -ge $size[0]*0.5 -and $end.X -le $size[0]-79) 'Collective must stay in right half'
    Check ($corner.Y -ge 0 -and $end.Y -le $size[1]*0.83) 'Collective must preserve hotbar'
    $topEnd=$instruments.WidgetToScreen($instruments.ActualSize)
    Check ($corner.Y -gt $topEnd.Y) 'Cockpit panels overlap'
    $p=$lever.WidgetToScreen((New-Object Engine.Vector2(59,126)))
    Check ([object]::ReferenceEquals($root.HitTestGlobal($p,$null),$lever)) 'Grip visual must hit the real collective'
}
if ($PreviewPath) {
    # Layout-derived offline preview, not a game screenshot; real font atlas and widget bounds.
    $root.Size=New-Object Engine.Vector2(1280,720)
    for ($i=0;$i -lt 3;$i++) {
        [Game.Widget]::LayoutWidgetsHierarchy($root,$root.Size)
        $hud.Refresh(0.2,0.4,0.35,116.4,(New-Object Engine.Vector3(10.2,1.6,0)),$true,$mode,1)
    }
    [Game.Widget]::LayoutWidgetsHierarchy($root,$root.Size)
    $bitmap=New-Object Drawing.Bitmap(1280,720)
    $graphics=[Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([Drawing.Color]::FromArgb(30,40,49))
    $graphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $pen=New-Object Drawing.Pen([Drawing.Color]::FromArgb(36,53,65))
    for ($i=0;$i -lt 1280;$i+=40) { $graphics.DrawLine($pen,$i,0,$i,720) }
    for ($i=0;$i -lt 720;$i+=40) { $graphics.DrawLine($pen,0,$i,1280,$i) }
    foreach ($widget in $root.AllChildren) {
        if (-not $widget.IsVisibleGlobal) { continue }
        $start=$widget.WidgetToScreen([Engine.Vector2]::Zero)
        $end=$widget.WidgetToScreen($widget.ActualSize)
        if ($widget -is [Game.BevelledRectangleWidget]) {
            $c=$widget.CenterColor
            $brush=New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb($c.A,$c.R,$c.G,$c.B))
            $graphics.FillRectangle($brush,[single]$start.X,[single]$start.Y,[single]($end.X-$start.X),[single]($end.Y-$start.Y))
            $brush.Dispose()
        }
        if ($widget -is [Game.LabelWidget]) {
            # System.Drawing text is a preview approximation; layout assertions above
            # use the real game's Pericles metrics, not this desktop preview font.
            $drawingFont=New-Object Drawing.Font('Segoe UI',([single](24*$widget.FontScale*$widget.GlobalScale)),[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
            $c=$widget.Color
            $brush=New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb($c.A,$c.R,$c.G,$c.B))
            $graphics.DrawString($widget.Text,$drawingFont,$brush,[single]$start.X,[single]$start.Y)
            $brush.Dispose(); $drawingFont.Dispose()
        }
    }
    $caption=New-Object Drawing.Font('Segoe UI',14)
    $graphics.DrawString('V0.5 / OFFLINE WIDGET LAYOUT / SIMULATED TELEMETRY',$caption,[Drawing.Brushes]::LightSlateGray,28,650)
    $graphics.DrawString('NATIVE CONTROLS RESERVED',$caption,[Drawing.Brushes]::LightSlateGray,28,680)
    $bitmap.Save([IO.Path]::GetFullPath($PreviewPath),[Drawing.Imaging.ImageFormat]::Png)
    $caption.Dispose(); $pen.Dispose(); $graphics.Dispose(); $bitmap.Dispose(); $atlas.Dispose()
    $hud.Refresh(0.2,0,0,70,[Engine.Vector3]::Zero,$true,$mode,1)
    [Game.Widget]::LayoutWidgetsHierarchy($root,$root.Size)
}
[Engine.Input.Touch]::Clear()
Finger 1 Pressed $lever 59 126
Frame
Check $hud.IsDragging 'Grip captures initial finger'
[Engine.Input.Touch]::Clear()
Finger 1 Moved $lever 59 34
# Later touch elsewhere must not steal an existing collective drag.
Finger 2 Pressed $root 80 200
Frame
Check ($hud.RawValue -gt 0.99) 'Second finger stole collective'
[Engine.Input.Touch]::Clear()
Finger 1 Moved $lever 59 172
Finger 2 Moved $root 100 210
Frame
Check ($hud.RawValue -lt -0.49 -and $hud.RawValue -gt -0.51) 'Analog downward drag incorrect'
[Engine.Input.Touch]::Clear()
Finger 1 Released $lever 59 172
Finger 2 Moved $root 100 210
Frame
Check (-not $hud.IsDragging -and $hud.RawValue -eq 0) 'Release must clear drag'
[Engine.Input.Touch]::Clear()
Finger 3 Pressed $lever 59 10
Frame
Check (-not $hud.IsDragging) 'Rail tap away from grip must not jump collective'
[Engine.Input.Touch]::Clear()
Finger 3 Pressed $lever 59 126
Frame
[Engine.Input.Touch]::Clear()
Finger 3 Moved $lever -60 126
Frame
Check (-not $hud.IsDragging) 'Far slide-out must cancel capture'
[Engine.Input.Touch]::Clear()
Finger 4 Pressed $toggle 59 22
Frame
[Engine.Input.Touch]::Clear()
Finger 4 Released $toggle 59 22
Frame
Check ($hud.ConsumeHoverToggle()) 'Hover tap not delivered'
Check (-not $hud.ConsumeHoverToggle()) 'Hover tap delivered twice'
[Engine.Input.Touch]::Clear()
Finger 5 Pressed $lever 59 126
Frame
$root.WidgetsHierarchyInput.Clear()
Frame
Check (-not $hud.IsDragging) 'Input clear must cancel captured finger'
$hud.Detach()
Check ($root.Children.Count -eq 0) 'Dismount leaves HUD widgets behind'
Check (-not $hud.IsDragging) 'Dismount keeps stale drag'
Check ($hud.Attach($root)) 'Reattachment failed'
Check ($root.Children.Count -eq 1) 'Reattachment duplicates overlay'
$hud.Detach()
[Engine.Input.Touch]::Clear()
Write-Host "Helicopter target-assembly HUD checks passed: $script:count"
# Actual drive integration: no world position writes and no ground taxi/yaw.
$drive=[Activator]::CreateInstance($mod.GetType('SCIEPort.Features.AirVehicles.ComponentHelicopterDrive'))
$body=New-Object Game.ComponentBody
$body.Position=New-Object Engine.Vector3(0,70,0)
$body.Rotation=[Engine.Quaternion]::Identity
$seats=[Activator]::CreateInstance($mod.GetType('SCIEPort.Features.Vehicles.ComponentTwoSeatManager'))
function SetField($object,$name,$value) { $object.GetType().GetField($name,[Reflection.BindingFlags]'NonPublic,Instance').SetValue($object,$value) }
SetField $drive m_body $body
SetField $drive m_seats $seats
SetField $drive m_controlsAvailable $true
foreach ($entry in @(@('m_rotorSpeed',1),@('m_gravity',9.8),@('m_verticalResponse',2.8),@('m_ascentSpeed',4.5),@('m_descentSpeed',3.5),@('m_maximumWorldY',240),@('m_horizontalAcceleration',5.5),@('m_brakeAcceleration',8),@('m_horizontalDrag',1.5),@('m_lateralGrip',2.5),@('m_maximumForwardSpeed',12),@('m_maximumReverseSpeed',5),@('m_turnRate',1.2))) {
    SetField $drive $entry[0] ([single]$entry[1])
}
SetField $seats m_driverMove (New-Object Engine.Vector3(1,0,1))
$simulation=$drive.GetType().GetMethod('SimulateFlight',[Reflection.BindingFlags]'NonPublic,Instance')
$controller=Field $drive m_hover; $controller.Reset(70)
$body.Velocity=New-Object Engine.Vector3(3,0,2)
[void]$simulation.Invoke($drive,@([single]0.02,$true,$true))
Check ($body.Velocity.X -eq 0 -and $body.Velocity.Z -eq 0) 'Grounded helicopter taxis'
Check ($body.Rotation -eq [Engine.Quaternion]::Identity) 'Grounded helicopter yaws'
[void]$simulation.Invoke($drive,@([single]0.02,$false,$true))
Check ($body.Velocity.LengthSquared() -gt 0) 'Airborne direction input does not work'
Check ($body.Rotation -ne [Engine.Quaternion]::Identity) 'Airborne yaw does not work'
Check ($body.Position.Y -eq 70) 'Flight controller teleports world height'
Write-Host "Helicopter target-assembly total checks passed: $script:count"
