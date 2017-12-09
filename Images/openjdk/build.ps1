#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds the base ubuntu/openjdk:# image.
#
# Usage: powershell -file build.ps1 VERSION

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $registry,
	[parameter(Mandatory=$True,Position=2)][string] $version,
	[parameter(Mandatory=$True,Position=3)][string] $tag,
	[switch]$latest = $False
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

"   "
"======================================="
"* OpenJDK:$version"
"======================================="

$registry = "neoncluster/openjdk"

Exec { docker build -t "${registry}:$tag" --build-arg "VERSION=$version" . }

if ($latest)
{
	Exec { docker tag "${registry}:$tag" "${registry}:latest"}
}

# Clean up

Exec { DeleteFile .rnd }
