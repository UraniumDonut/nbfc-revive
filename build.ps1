$workingDir = split-path $MyInvocation.MyCommand.Path
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$nugeturl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"

function download-nuget {
	$client = New-Object System.Net.WebClient
	
	# Set proxy to null to prevent the web client from trying to retrieve
	# proxy settings from IE (which is incredibly slow)
	$client.Proxy = $null
	$client.DownloadFile($nugeturl, "$workingDir\nuget.exe")
}

push-location $workingDir

# download nuget if necessary
if(!(test-path .\nuget.exe)) {
	write-output "NuGet could not be found. Downloading latest recommended version."
	download-nuget
}

# restore nuget packages for solution
& .\nuget.exe restore

# re-download nuget and retry on error
if($LASTEXITCODE -ne 0) {	
	write-output "Packages could not be restored. Updating NuGet."
	remove-item .\nuget.exe
	download-nuget
	& .\nuget.exe restore
}

# get msbuild path
$path = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$msbuild = join-path $path 'MSBuild\Current\Bin\MSBuild.exe'

# use the WiX Toolset targets if WiX is installed system wide
# (VS Build Tools don't look for them in the default location)
$wixArgs = @()
$wixTargetsDir = "${env:ProgramFiles(x86)}\MSBuild\Microsoft\WiX\v3.x"

if(test-path "$wixTargetsDir\Wix.targets") {
	$wixArgs = "/p:WixTargetsPath=$wixTargetsDir\Wix.targets", "/p:WixCATargetsPath=$wixTargetsDir\Wix.CA.targets"
}

# build solution
& $msbuild /t:Clean,Build /p:Configuration=ReleaseWindows $wixArgs NoteBookFanControl.sln
$buildResult = $LASTEXITCODE

pop-location

exit $buildResult
