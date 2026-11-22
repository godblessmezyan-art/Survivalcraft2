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
Add-Type -AssemblyName System.Xml.Linq
$databaseReader = New-Object IO.StreamReader($zip.GetEntry('Assets/Database.xml').Open())
try { $targetDatabase = [System.Xml.Linq.XElement]::Parse($databaseReader.ReadToEnd()) }
finally { $databaseReader.Dispose() }
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

# Exercise the mod against real SCNET camera and renderer components, with the packaged model's bone transforms.
$asset = Join-Path (Split-Path -Parent $PSScriptRoot) 'src/SCIEPort/Assets/Models/Helicopter.dae'
$xdb = Join-Path (Split-Path -Parent $PSScriptRoot) 'src/SCIEPort/Assets/Database/Helicopter.xdb'
$stream = [IO.File]::OpenRead($xdb)
try { [ModsManager]::CombineDataBase($targetDatabase, $stream) } finally { $stream.Dispose() }
$database = [TemplatesDatabase.XmlDatabaseSerializer]::LoadDatabase($targetDatabase)
$gameDatabase = New-Object GameEntitySystem.GameDatabase($database)
$helicopterValues = New-Object TemplatesDatabase.ValuesDictionary
$helicopterValues.PopulateFromDatabaseObject($database.FindDatabaseObject('SCIEPortHelicopter',$gameDatabase.EntityTemplateType,$true))
Check ($helicopterValues['SimpleModel']['Class'] -eq 'SCIEPort.Features.AirVehicles.ComponentHelicopterModel' -and
    $helicopterValues['SimpleModel']['ModelName'] -eq 'Models/Helicopter') 'Native database merge must select the cockpit model for the original entity'
$stream = [IO.File]::OpenRead($asset)
try { $data = [Engine.Media.ModelData]::Load($stream) } finally { $stream.Dispose() }
$runtimeModel = New-Object Engine.Graphics.Model
foreach ($bone in $data.Bones) {
    $parent = if ($bone.ParentBoneIndex -ge 0) { $runtimeModel.Bones[$bone.ParentBoneIndex] }
    [void]$runtimeModel.NewBone($bone.Name, $bone.Transform, $parent)
}
$glass = $runtimeModel.FindBone('CanopyGlass')
$visual = $runtimeModel.FindBone('VisualRoot')
SetField $drive m_visualRootBone $visual
SetField $drive m_visualPitch ([single]0.09)
SetField $drive m_visualRoll ([single]-0.11)
$damage = New-Object Game.ComponentDamage
$damage.Hitpoints = 1
SetField $drive m_damage $damage
$body.Velocity = New-Object Engine.Vector3(6,2.25,0)
SetField $drive m_rotorSpeed ([single]0.8)
$gauges = $drive.GetType().GetProperty('CockpitGaugeReadings',[Reflection.BindingFlags]'NonPublic,Instance')
$readings = $gauges.GetValue($drive)
Check ([Engine.Vector3]::DistanceSquared($readings,(New-Object Engine.Vector3(0.5,0.75,0.8))) -lt 0.000001) 'Gauges must read horizontal speed, climb and rotor power'
$body.Velocity = New-Object Engine.Vector3(0,-1.75,0)
$readings = $gauges.GetValue($drive)
Check ([Math]::Abs($readings.Y - 0.25) -lt 0.0001) 'Vertical speed gauge must move below center for descent'
$entity = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([GameEntitySystem.Entity])
$components = New-Object 'System.Collections.Generic.List[GameEntitySystem.Component]'
$components.Add($drive)
$components.Add($body)
SetField $entity m_components $components
$body.m_entity = $drive.m_entity = $entity
$model = [Activator]::CreateInstance($mod.GetType('SCIEPort.Features.AirVehicles.ComponentHelicopterModel', $true))
$model.m_entity = $entity
$model.m_componentFrame = $body
$model.m_subsystemSky = New-Object Game.SubsystemSky
$model.Model = $runtimeModel
$renderer = New-Object Game.SubsystemModelsRenderer
SetField $model m_renderer $renderer

$player = New-Object Game.ComponentPlayer
$player.ComponentBody = New-Object Game.ComponentBody
$player.ComponentBody.ParentBody = $body
$player.ComponentHealth = New-Object Game.ComponentHealth
$player.ComponentHealth.Health = 1
$player.ComponentLocomotion = New-Object Game.ComponentLocomotion
$player.ComponentLocomotion.LookAngles = New-Object Engine.Vector2(0.2,-0.15)
$player.ComponentInput = New-Object Game.ComponentInput
$player.ComponentRider = New-Object Game.ComponentRider
$widget = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Game.GameWidget])
$widget.Target = $player
$camera = New-Object Game.FppCamera($widget)
$outside = New-Object Game.TppCamera($widget)
$widget.m_activeCamera = $camera
$updateView = $mod.GetType('SCIEPort.Features.AirVehicles.HelicopterCockpit', $true).GetMethod('UpdateView',[Reflection.BindingFlags]'NonPublic,Static')

foreach ($seat in @('Driver', 'Passenger')) {
    $property = $seats.GetType().GetProperty($seat)
    $property.SetValue($seats, $player.ComponentRider)
    $column = if ($seat -eq 'Driver') { -0.42 } else { 0.42 }
    $visualTransform = [Engine.Matrix]::CreateRotationX(0.09) * [Engine.Matrix]::CreateRotationZ(-0.11) * $visual.Transform * $body.Matrix
    $expected = [Engine.Matrix]::CreateFromYawPitchRoll(-0.2,-0.15,0) * $visualTransform
    $eye = [Engine.Vector3]::Transform((New-Object Engine.Vector3($column,0.33,-0.08)), $visualTransform)
    [void]$updateView.Invoke($null, @($camera.PSObject.BaseObject))
    Check ([Engine.Vector3]::DistanceSquared($camera.ViewPosition, $eye) -lt 0.000001) "Cockpit eye is outside the $seat seat"
    Check ([Engine.Vector3]::Dot($camera.ViewDirection, $expected.Forward) -gt 0.9999) "Cockpit $seat lost look input or aircraft yaw"
    Check ([Engine.Vector3]::Dot($camera.ViewUp, $expected.Up) -gt 0.9999) "Cockpit $seat tilt differs from the model"
    $model.CalculateIsVisible($camera)
    Check ($model.IsVisibleForCamera -and $model.GetBoneTransform($glass.Index).M11 -eq 0) "Canopy blocks the $seat view"
    $renderer.PrimitivesRenderer.Clear()
    $model.DrawExtras($camera)
    $batch = $renderer.PrimitivesRenderer.FlatBatch(0,[Engine.Graphics.DepthStencilState]::DepthRead,[Engine.Graphics.RasterizerState]::CullNoneScissor,[Engine.Graphics.BlendState]::Opaque)
    Check ($batch.TriangleIndices.Count -eq 18 -and $batch.TriangleVertices.Array[0].Position.Z -lt -0.1) 'Live needles must follow the panel in camera space'

    $widget.m_activeCamera = $outside
    $model.CalculateIsVisible($outside)
    Check ($null -eq $model.GetBoneTransform($glass.Index)) 'Exterior view inherited the hidden glass'
    $widget.m_activeCamera = $camera
    $property.SetValue($seats, $null)
}
$seats.GetType().GetProperty('Driver').SetValue($seats, $player.ComponentRider)
$stockEye = New-Object Engine.Vector3(0,72,0)
foreach ($reason in @('dismount animation', 'detached', 'VR', 'dead')) {
    $player.ComponentRider.m_isAnimating = $reason -eq 'dismount animation'
    $player.ComponentBody.ParentBody = if ($reason -ne 'detached') { $body }
    $player.ComponentInput.IsControlledByVr = $reason -eq 'VR'
    $player.ComponentHealth.Health = [single]($reason -ne 'dead')
    $camera.SetupPerspectiveCamera($stockEye, (New-Object Engine.Vector3(0,0,-1)), [Engine.Vector3]::UnitY)
    [void]$updateView.Invoke($null, @($camera.PSObject.BaseObject))
    $model.CalculateIsVisible($camera)
    Check ($camera.ViewPosition -eq $stockEye -and $null -eq $model.GetBoneTransform($glass.Index)) "Cockpit persisted during $reason"
}
Write-Host "Helicopter target-assembly total checks passed: $script:count"
