﻿#------------------------------------------------------------------------------
# FILE:         includes.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Misc image build related utilities.

#------------------------------------------------------------------------------
# Important source code paths.

$src_path          = $env:NF_ROOT
$src_lib_path      = "$src_path\\Lib"
$src_services_path = "$src_path\\Services"
$src_tools_path    = "$src_path\\Tools"

#------------------------------------------------------------------------------
# Global constants.

$tini_version = "v0.13.2"              # TINI init manager version

#------------------------------------------------------------------------------
# Executes a command, throwing an exception for non-zero error codes.

function Exec
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [scriptblock]$Command,
        [Parameter(Position=1, Mandatory=0)]
        [string]$ErrorMessage = "*** FAILED: $Command"
    )
    & $Command
    if ($LastExitCode -ne 0) {
        throw "Exec: $ErrorMessage"
    }
}

#------------------------------------------------------------------------------
# Deletes a file if it exists.

function DeleteFile
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$Path
    )

	if (Test-Path $Path) 
	{ 
		Remove-Item $Path 
	} 
}

#------------------------------------------------------------------------------
# Pushes a Docker image to the public registry with retry as an attempt to handle
# transient registry issues.

function PushImage
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$Image
    )

	$maxAttempts = 5

	for ($attempt=0; $attempt -lt $maxAttempts; $attempt++)
	{
		& docker push "$Image"

		if ($LastExitCode -eq 0) {
			return
		}
		
		sleep 15
	}

	throw "[docker push $Image] failed after [$maxAttempts] attempts."
}

#------------------------------------------------------------------------------
# Returns the current date (UTC) formatted as "yyyyMMdd".

function UtcDate
{
	return [datetime]::UtcNow.ToString('yyyyMMdd')
}

#------------------------------------------------------------------------------
# Returns the current Git branch.

function GitBranch
{
	$branch = git rev-parse --abbrev-ref HEAD

	return $branch
}

#------------------------------------------------------------------------------
# Returns the current Git branch, date, and commit formatted as a Docker image tag.

function ImageTag
{
	$branch = GitBranch
	$date   = UtcDate
	$commit = git log -1 --pretty=%h

	return "$branch-$date-$commit"
}

#------------------------------------------------------------------------------
# Returns $True if the current Git branch is "prod".

function IsProd
{
	$branch = git rev-parse --abbrev-ref HEAD

	return $branch -eq "prod"
}

#------------------------------------------------------------------------------
# Makes any text files that will be included in Docker images Linux safe, by
# converting CRLF line endings to LF and replacing TABs with spaces.

exec { unix-text --recursive $image_root\Dockerfile }
exec { unix-text --recursive $image_root\*.sh }
exec { unix-text --recursive $image_root\*.yml }
exec { unix-text --recursive .\*.cfg }
exec { unix-text --recursive .\*.js }
exec { unix-text --recursive .\*.conf }
exec { unix-text --recursive .\*.md }
exec { unix-text --recursive .\*.json }
exec { unix-text --recursive .\*.rb }
