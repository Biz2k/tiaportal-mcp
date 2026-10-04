Add-Type -Path 'C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\Siemens.Engineering.dll'
[AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.FullName -like '*Siemens.Engineering*' } | ForEach-Object { $_.GetTypes() } | Where-Object { $_.Name -match 'Simulation' } | Select-Object FullName
