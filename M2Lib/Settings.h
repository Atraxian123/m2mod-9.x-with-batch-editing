#pragma once

#include "BaseTypes.h"
#include "M2Types.h"

namespace M2Lib
{
#pragma pack(push, 1)
	struct M2LIB_API_CLASS Settings
	{
		wchar_t OutputDirectory[1024];
		wchar_t WorkingDirectory[1024];
		wchar_t MappingsDirectory[1024];
		Expansion ForceLoadExpansion = Expansion::None;
		uint32_t CustomFilesStartIndex = 0;
		bool MergeBones = true;
		bool MergeAttachments = true;
		bool MergeCameras = true;
		bool FixSeams = false;
		bool FixEdgeNormals = true;
		bool IgnoreOriginalMeshIndexes = false;
		bool FixAnimationsTest = false;

		// When true, .skin file names are always derived from the classic
		// "<model>0N.skin" / "<model>_LOD0N.skin" naming convention, even if the
		// model has an SFID chunk and a listfile entry is available for its
		// FileDataId. When false (default), the retail FileDataId/listfile
		// lookup is tried first and this convention is only used as a fallback.
		bool UseFallbackSkinNaming = false;

		void setOutputDirectory(const wchar_t* directory);
		void setWorkingDirectory(const wchar_t* directory);
		void setMappingsDirectory(const wchar_t* directory);

		Settings()
		{
			setOutputDirectory(L"");
			setWorkingDirectory(L"");
			setMappingsDirectory(L"");
		}

		void operator=(Settings const& other);
	};

	ASSERT_SIZE(Settings, 1024 * 2 * 2 + sizeof(wchar_t) * 1024 + 4 + 7 + 4 + 1);
#pragma pack(pop)
}
