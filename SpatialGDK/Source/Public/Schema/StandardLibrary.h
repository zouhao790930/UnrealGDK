// Copyright (c) Improbable Worlds Ltd, All Rights Reserved

#pragma once

#include "Platform.h"

#include "Schema/Component.h"
#include "Utils/SchemaUtils.h"

#include <improbable/c_schema.h>
#include <improbable/c_worker.h>

struct Coordinates
{
	double X;
	double Y;
	double Z;

	inline static Coordinates FromFVector(const FVector& Location)
	{
		Coordinates Coords;
		Coords.X = 0.01 * Location.Y;
		Coords.Y = 0.01 * Location.Z;
		Coords.Z = 0.01 * Location.X;

		return Coords;
	}

	inline static FVector ToFVector(const Coordinates& Coords)
	{
		FVector Location;
		Location.X = 100.0 * Coords.Z;
		Location.Y = 100.0 * Coords.X;
		Location.Z = 100.0 * Coords.Y;

		return Location;
	}
};

const Worker_ComponentId POSITION_COMPONENT_ID = 54;

struct SpatialPosition : SpatialComponent
{
	static const Worker_ComponentId ComponentId = POSITION_COMPONENT_ID;

	SpatialPosition() = default;

	SpatialPosition(const Coordinates& InCoords)
		: Coords(InCoords) {}

	SpatialPosition(const Worker_ComponentData& Data)
	{
		Schema_Object* ComponentObject = Schema_GetComponentDataFields(Data.schema_type);

		Schema_Object* CoordsObject = Schema_GetObject(ComponentObject, 1);

		Coords.X = Schema_GetDouble(CoordsObject, 1);
		Coords.Y = Schema_GetDouble(CoordsObject, 2);
		Coords.Z = Schema_GetDouble(CoordsObject, 3);
	}

	Worker_ComponentData CreatePositionData()
	{
		Worker_ComponentData Data = {};
		Data.component_id = POSITION_COMPONENT_ID;
		Data.schema_type = Schema_CreateComponentData(POSITION_COMPONENT_ID);
		Schema_Object* ComponentObject = Schema_GetComponentDataFields(Data.schema_type);

		Schema_Object* CoordsObject = Schema_AddObject(ComponentObject, 1);

		Schema_AddDouble(CoordsObject, 1, Coords.X);
		Schema_AddDouble(CoordsObject, 2, Coords.Y);
		Schema_AddDouble(CoordsObject, 3, Coords.Z);

		return Data;
	}

	static Worker_ComponentUpdate CreatePositionUpdate(const Coordinates& Coords)
	{
		Worker_ComponentUpdate ComponentUpdate = {};
		ComponentUpdate.component_id = POSITION_COMPONENT_ID;
		ComponentUpdate.schema_type = Schema_CreateComponentUpdate(POSITION_COMPONENT_ID);
		Schema_Object* ComponentObject = Schema_GetComponentUpdateFields(ComponentUpdate.schema_type);

		Schema_Object* CoordsObject = Schema_AddObject(ComponentObject, 1);

		Schema_AddDouble(CoordsObject, 1, Coords.X);
		Schema_AddDouble(CoordsObject, 2, Coords.Y);
		Schema_AddDouble(CoordsObject, 3, Coords.Z);

		return ComponentUpdate;
	}

	Coordinates Coords;
};

using WriteAclMap = TMap<Worker_ComponentId, WorkerRequirementSet>;

const Worker_ComponentId ENTITY_ACL_COMPONENT_ID = 50;

struct SpatialEntityAcl : SpatialComponent
{
	static const Worker_ComponentId ComponentId = ENTITY_ACL_COMPONENT_ID;

	SpatialEntityAcl() = default;

	SpatialEntityAcl(const WorkerRequirementSet& InReadAcl, const WriteAclMap& InComponentWriteAcl)
		: ReadAcl(InReadAcl), ComponentWriteAcl(InComponentWriteAcl) {}

	SpatialEntityAcl(const Worker_ComponentData& Data)
	{
		Schema_Object* ComponentObject = Schema_GetComponentDataFields(Data.schema_type);

		ReadAcl = Schema_GetWorkerRequirementSet(ComponentObject, 1);

		uint32 KVPairCount = Schema_GetObjectCount(ComponentObject, 2);
		for (uint32 i = 0; i < KVPairCount; i++)
		{
			Schema_Object* KVPairObject = Schema_IndexObject(ComponentObject, 2, i);
			uint32 Key = Schema_GetUint32(KVPairObject, SCHEMA_MAP_KEY_FIELD_ID);
			WorkerRequirementSet Value = Schema_GetWorkerRequirementSet(KVPairObject, SCHEMA_MAP_VALUE_FIELD_ID);

			ComponentWriteAcl.Add(Key, Value);
		}
	}

	void ApplyComponentUpdate(const Worker_ComponentUpdate& Update)
	{
		Schema_Object* ComponentObject = Schema_GetComponentUpdateFields(Update.schema_type);

		if (Schema_GetObjectCount(ComponentObject, 1) > 0)
		{
			ReadAcl = Schema_GetWorkerRequirementSet(ComponentObject, 1);
		}

		// This is never emptied, so does not need an additional check for cleared fields
		uint32 KVPairCount = Schema_GetObjectCount(ComponentObject, 2);
		if (KVPairCount > 0)
		{
			ComponentWriteAcl.Empty();
			for (uint32 i = 0; i < KVPairCount; i++)
			{
				Schema_Object* KVPairObject = Schema_IndexObject(ComponentObject, 2, i);
				uint32 Key = Schema_GetUint32(KVPairObject, SCHEMA_MAP_KEY_FIELD_ID);
				WorkerRequirementSet Value = Schema_GetWorkerRequirementSet(KVPairObject, SCHEMA_MAP_VALUE_FIELD_ID);

				ComponentWriteAcl.Add(Key, Value);
			}
		}
	}

	Worker_ComponentData CreateEntityAclData()
	{
		Worker_ComponentData Data = {};
		Data.component_id = ENTITY_ACL_COMPONENT_ID;
		Data.schema_type = Schema_CreateComponentData(ENTITY_ACL_COMPONENT_ID);
		Schema_Object* ComponentObject = Schema_GetComponentDataFields(Data.schema_type);

		Schema_AddWorkerRequirementSet(ComponentObject, 1, ReadAcl);

		for (const auto& KVPair : ComponentWriteAcl)
		{
			Schema_Object* KVPairObject = Schema_AddObject(ComponentObject, 2);
			Schema_AddUint32(KVPairObject, SCHEMA_MAP_KEY_FIELD_ID, KVPair.Key);
			Schema_AddWorkerRequirementSet(KVPairObject, SCHEMA_MAP_VALUE_FIELD_ID, KVPair.Value);
		}

		return Data;
	}

	Worker_ComponentUpdate CreateEntityAclUpdate()
	{
		Worker_ComponentUpdate ComponentUpdate = {};
		ComponentUpdate.component_id = ENTITY_ACL_COMPONENT_ID;
		ComponentUpdate.schema_type = Schema_CreateComponentUpdate(ENTITY_ACL_COMPONENT_ID);
		Schema_Object* ComponentObject = Schema_GetComponentUpdateFields(ComponentUpdate.schema_type);

		Schema_AddWorkerRequirementSet(ComponentObject, 1, ReadAcl);

		for (const auto& KVPair : ComponentWriteAcl)
		{
			Schema_Object* KVPairObject = Schema_AddObject(ComponentObject, 2);
			Schema_AddUint32(KVPairObject, SCHEMA_MAP_KEY_FIELD_ID, KVPair.Key);
			Schema_AddWorkerRequirementSet(KVPairObject, SCHEMA_MAP_VALUE_FIELD_ID, KVPair.Value);
		}

		return ComponentUpdate;
	}

	WorkerRequirementSet ReadAcl;
	WriteAclMap ComponentWriteAcl;
};

const Worker_ComponentId METADATA_COMPONENT_ID = 53;

struct SpatialMetadata : SpatialComponent
{
	static const Worker_ComponentId ComponentId = METADATA_COMPONENT_ID;

	SpatialMetadata() = default;

	SpatialMetadata(const FString& InEntityType)
		: EntityType(InEntityType) {}

	SpatialMetadata(const Worker_ComponentData& Data)
	{
		Schema_Object* ComponentObject = Schema_GetComponentDataFields(Data.schema_type);

		EntityType = Schema_GetString(ComponentObject, 1);
	}

	Worker_ComponentData CreateMetadataData()
	{
		Worker_ComponentData Data = {};
		Data.component_id = METADATA_COMPONENT_ID;
		Data.schema_type = Schema_CreateComponentData(METADATA_COMPONENT_ID);
		Schema_Object* ComponentObject = Schema_GetComponentDataFields(Data.schema_type);

		Schema_AddString(ComponentObject, 1, EntityType);

		return Data;
	}

	FString EntityType;
};

const Worker_ComponentId PERSISTENCE_COMPONENT_ID = 55;

struct SpatialPersistence : SpatialComponent
{
	static const Worker_ComponentId ComponentId = PERSISTENCE_COMPONENT_ID;

	SpatialPersistence() = default;
	SpatialPersistence(const Worker_ComponentData& Data)
	{
	}

	FORCEINLINE Worker_ComponentData CreatePersistenceData()
	{
		Worker_ComponentData Data = {};
		Data.component_id = PERSISTENCE_COMPONENT_ID;
		Data.schema_type = Schema_CreateComponentData(PERSISTENCE_COMPONENT_ID);

		return Data;
	}
};