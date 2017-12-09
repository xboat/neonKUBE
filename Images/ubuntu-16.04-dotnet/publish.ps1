#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds all of the supported Ubuntu/.NET Core images and pushes them to Docker Hub.
#
# NOTE: You must be logged into Docker Hub.
#
# Usage: powershell -file ./publish.ps1

param 
(
	[switch]$all = $False
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

function Build
{
	param
	(
		[parameter(Mandatory=$True, Position=1)][string] $dotnetVersion,
		[switch]$latest = $False
	)

	$registry = "neoncluster/ubuntu-16.04-dotnet"
	$date     = UtcDate
	$tag      = "${dotnetVersion}-${date}"

	# Build the images.

	if ($latest)
	{
		./build.ps1 -registry $registry -tag $tag -dotnetVersion $dotnetVersion -latest
	}
	else
	{
		./build.ps1 -registry $registry -tag $tag -dotnetVersion $dotnetVersion 
	}

	PushImage "${registry}:$tag"

	if ($latest)
	{
		PushImage "${registry}:latest"
	}
}

if ($all)
{
}

Build 2.0.3 -latest
