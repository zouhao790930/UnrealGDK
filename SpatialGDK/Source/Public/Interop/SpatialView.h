// Copyright (c) Improbable Worlds Ltd, All Rights Reserved

#pragma once

#include "CoreMinimal.h"

#include "Schema/UnrealMetadata.h"
#include "Schema/StandardLibrary.h"
#include "SpatialConstants.h"

#include <improbable/c_schema.h>
#include <improbable/c_worker.h>

#include "SpatialView.generated.h"

class USpatialNetDriver;
class USpatialReceiver;
class USpatialSender;

UCLASS()
class SPATIALGDK_API USpatialView : public UObject
{
	GENERATED_BODY()

public:
	void Init(USpatialNetDriver* NetDriver);
	void ProcessOps(Worker_OpList* OpList);

	Worker_Authority GetAuthority(Worker_EntityId EntityId, Worker_ComponentId ComponentId);
	SpatialUnrealMetadata* GetUnrealMetadata(Worker_EntityId EntityId);
	SpatialEntityAcl* GetEntityACL(Worker_EntityId EntityId);

private:
	void OnAddComponent(const Worker_AddComponentOp& Op);
	void OnRemoveEntity(const Worker_RemoveEntityOp& Op);
	void OnComponentUpdate(const Worker_ComponentUpdateOp& Op);
	void OnAuthorityChange(const Worker_AuthorityChangeOp& Op);

	USpatialNetDriver* NetDriver;
	USpatialReceiver* Receiver;
	USpatialSender* Sender;

	TMap<Worker_EntityId_Key, TMap<Worker_ComponentId, Worker_Authority>> EntityComponentAuthorityMap;
	TMap<Worker_EntityId_Key, TSharedPtr<SpatialUnrealMetadata>> EntityUnrealMetadataMap;
	TMap<Worker_EntityId_Key, TSharedPtr<SpatialEntityAcl>> EntityACLMap;
};