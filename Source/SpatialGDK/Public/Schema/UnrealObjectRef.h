// Copyright (c) Improbable Worlds Ltd, All Rights Reserved

#pragma once

#include "SchemaOption.h"

struct UnrealObjectRef
{
	UnrealObjectRef() = default;

	UnrealObjectRef(Worker_EntityId Entity, uint32 Offset)
		: Entity(Entity)
		, Offset(Offset)
	{}

	UnrealObjectRef(Worker_EntityId Entity, uint32 Offset, FString Path, UnrealObjectRef Outer)
		: Entity(Entity)
		, Offset(Offset)
		, Path(Path)
		, Outer(Outer)
	{}

	UnrealObjectRef(const UnrealObjectRef& In)
		: Entity(In.Entity)
		, Offset(In.Offset)
		, Path(In.Path)
		, Outer(In.Outer)
	{}

	FORCEINLINE UnrealObjectRef& operator=(const UnrealObjectRef& In)
	{
		Entity = In.Entity;
		Offset = In.Offset;
		Path = In.Path;
		Outer = In.Outer;
		return *this;
	}

	FORCEINLINE FString ToString() const
	{
		return FString::Printf(TEXT("(entity ID: %lld, offset: %u)"), Entity, Offset);
	}

	FORCEINLINE bool operator==(const UnrealObjectRef& Other) const
	{
		return Entity == Other.Entity &&
			Offset == Other.Offset &&
			((!Path && !Other.Path) || (Path && Other.Path && Path->Equals(*Other.Path))) &&
			((!Outer && !Other.Outer) || (Outer && Other.Outer && *Outer == *Other.Outer));
	}

	FORCEINLINE bool operator!=(const UnrealObjectRef& Other) const
	{
		return !operator==(Other);
	}

	friend uint32 GetTypeHash(const UnrealObjectRef& ObjectRef);

	Worker_EntityId Entity;
	uint32 Offset;
	TSchemaOption<FString> Path;
	TSchemaOption<UnrealObjectRef> Outer;
};

inline uint32 GetTypeHash(const UnrealObjectRef& ObjectRef)
{
	uint32 Result = 1327u;
	Result = (Result * 977u) + GetTypeHash(static_cast<int64>(ObjectRef.Entity));
	Result = (Result * 977u) + GetTypeHash(ObjectRef.Offset);
	Result = (Result * 977u) + (ObjectRef.Path ? 1327u * (GetTypeHash(*ObjectRef.Path) + 977u) : 977u);
	Result = (Result * 977u) + (ObjectRef.Outer ? 1327u * (GetTypeHash(*ObjectRef.Outer) + 977u) : 977u);
	return Result;
}
