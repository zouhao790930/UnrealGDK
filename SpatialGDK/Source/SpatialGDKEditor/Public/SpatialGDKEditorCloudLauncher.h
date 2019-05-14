// Copyright (c) Improbable Worlds Ltd, All Rights Reserved

#pragma once

#include "Misc/Paths.h"
#include "Logging/LogMacros.h"

DECLARE_LOG_CATEGORY_EXTERN(LogSpatialGDKCloudLauncher, Log, All);

const FString DeploymentLauncherAbsolutePath = FPaths::ConvertRelativePathToFull(FPaths::Combine(FPaths::ProjectDir() / TEXT("Plugins/UnrealGDK/SpatialGDK/Binaries/ThirdParty/Improbable/Programs/DeploymentLauncher")));

SPATIALGDKEDITOR_API bool SpatialGDKCloudLaunch();

SPATIALGDKEDITOR_API bool SpatialGDKCloudStop();