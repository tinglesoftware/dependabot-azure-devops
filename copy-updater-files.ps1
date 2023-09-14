Param(
    [string] $tag = "v0.230.0"
)

$hash = [ordered]@{
    ".ruby-version"                               = "../.ruby-version"

    "updater/lib/dependabot/environment.rb"       = "lib/dependabot/environment.rb"
    "updater/spec/dependabot/environment_spec.rb" = "spec/dependabot/environment_spec.rb"
    # "updater/spec/spec_helper.rb"                 = "spec/spec_helper.rb"
}

$baseUrl = "https://raw.githubusercontent.com/dependabot/dependabot-core"
$destinationFolder = Join-Path -Path '.' -ChildPath 'updater'

foreach ($h in $hash.GetEnumerator()) {
    $sourceUrl = "$baseUrl/$tag/$($h.Name)"
    $destinationPath = Join-Path -Path "$destinationFolder" -ChildPath "$($h.Value)"
    Write-Host "`Downloading $($h.Name) ..."
    [System.IO.Directory]::CreateDirectory("$(Split-Path -Path "$destinationPath")") | Out-Null
    Invoke-WebRequest -Uri $sourceUrl -OutFile $destinationPath
}
