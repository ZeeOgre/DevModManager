#ifndef GAMEMANAGER_H
#define GAMEMANAGER_H

#include "libfo76utils/src/common.hpp"

#include <unordered_map>
#include <QString>
#include <QStringList>

class QProgressDialog;
class NifModel;
class BA2File;
class CE2MaterialDB;

namespace Game
{

enum GameMode : int
{
	OTHER,
	MORROWIND,
	OBLIVION,
	// Fallout 3 and Fallout NV cannot be differentiated by version
	FALLOUT_3NV,
	SKYRIM,
	SKYRIM_SE,
	FALLOUT_4,
	FALLOUT_76,
	STARFIELD,

	NUM_GAMES
};

QString StringForMode(GameMode game);
GameMode ModeForString(QString game);

class GameManager
{
	GameManager();
public:
	GameManager( const GameManager & ) = delete;
	GameManager & operator= ( const GameManager ) = delete;

	// OTHER is returned if 'nif' is nullptr
	static GameMode get_game( const NifModel * nif );

	static GameManager * get();

	//! Game installation path
	static QString path( const GameMode game );
	//! Game data path
	static QString data( const GameMode game );
	//! Game folders managed by the GameManager
	static QStringList folders( const GameMode game );
	//! Game enabled status in the GameManager
	static bool status( const GameMode game );

	struct GameResources
	{
		GameMode	game = OTHER;
		std::int32_t	refCnt = 0;
		BA2File *	ba2File = nullptr;
		CE2MaterialDB *	sfMaterials = nullptr;
		std::uint64_t	sfMaterialDB_ID = 0;
		GameResources *	parent = nullptr;
		// list of data paths, empty for archived NIFs
		QStringList	dataPaths;
		~GameResources();
		void init_archives();
		CE2MaterialDB * init_materials();
		void close_archives();
		void close_materials();
		QString find_file( const std::string_view & fullPath );
		bool get_file( QByteArray & data, const std::string_view & fullPath );
		void list_files(
			std::set< std::string_view > & fileSet,
			bool (*fileListFilterFunc)( void * p, const std::string_view & fileName ), void * fileListFilterFuncData );
	};

	static GameResources * addNIFResourcePath( const NifModel * nif, const QString & dataPath );
	static void removeNIFResourcePath( const NifModel * nif );
	static inline GameResources & getNIFResources( const NifModel * nif );
	static inline GameResources & getGameResources( const GameMode game );

	//! Convert 'name' to lower case, replace backslashes with forward slashes, and make sure that the path
	// begins with 'archive_folder' and ends with 'extension' (e.g. "textures" and ".dds").
	static std::string get_full_path( const QString & name, const char * archive_folder, const char * extension );
	//! Search for file 'path' in the resource archives and folders, and return the full path if the file is found,
	// or an empty string otherwise.
	static QString find_file(
		const GameMode game, const QString & path, const char * archiveFolder, const char * extension );
	//! Find and load resource file to 'data'. The return value is true on success.
	static bool get_file( QByteArray & data, const GameMode game, const std::string_view & fullPath );
	static bool get_file(
		QByteArray & data, const GameMode game,
		const QString & path, const char * archiveFolder, const char * extension );
	//! Return pointer to Starfield material database, loading it first if necessary.
	// On error, nullptr is returned.
	static CE2MaterialDB * materials( const GameMode game );
	//! Returns a unique ID for the currently loaded material database (0 if none).
	// Previously returned material pointers become invalid when this value changes.
	static std::uint64_t get_material_db_id( const GameMode game );
	//! Close all currently opened resource archives, files and materials. If 'nifResourcesFirst' is true,
	// then only the resources associated with loose NIF files are closed, if there are any.
	static void close_resources( bool nifResourcesFirst = false );
	//! List resource files available for 'game' on the archive filesystem, as a set of null-terminated strings.
	// The file list can be optionally filtered by a function that returns false if the file should be excluded.
	static void list_files(
		std::set< std::string_view > & fileSet, const GameMode game,
		bool (*fileListFilterFunc)( void * p, const std::string_view & fileName ) = nullptr,
		void * fileListFilterFuncData = nullptr );

	//! Return a sorted list of .bsa and .ba2 files (base name only) under dataPath
	static QStringList get_archive_list( const QString & dataPath );
	//! Find applicable data folders and archives at the game installation path
	static QStringList find_paths( const GameMode game );
	//! Find applicable data folders and archives under dataPath
	static QStringList find_paths( const GameMode game, const QString & dataPath );
	//! Remove invalid paths (if game is valid) and duplicates
	static void remove_invalid_paths( QStringList & dataPaths, const GameMode game = NUM_GAMES );

	//! Game installation path
	static inline QString path( const QString & game );
	//! Game folders managed by the GameManager
	static inline QStringList folders( const QString & game );
	//! Game enabled status in the GameManager
	static inline bool status( const QString & game );

	static inline void update_game( const GameMode game, const QString & path );
	static inline void update_game( const QString & game, const QString & path );
	static inline void update_folders( const GameMode game, const QStringList & list );
	static inline void update_status( const GameMode game, bool status );
	static inline void update_status( const QString & game, bool status );
	static inline void update_other_games_fallback( bool status );
	static inline void update_ignore_archive_errors( bool status );

	static void init_settings( int & manager_version, QProgressDialog * dlg = nullptr );
	static void update_settings( int & manager_version, QProgressDialog * dlg = nullptr );

	//! Save the manager to settings
	static void save();
	//! Load the manager from settings
	static void load();
	//! Reset the manager
	static void clear();

private:
	static void insert_game( const GameMode game, const QString & path );
	static void insert_folders( const GameMode game, const QStringList & list );
	static void insert_status( const GameMode game, bool status );

	static GameResources	archives[NUM_GAMES];
	// resources associated with loose NIF files
	static std::unordered_map< const NifModel *, GameResources * >	nifResourceMap;
	static std::uint64_t	material_db_prv_id;
	static QString	gamePaths[NUM_GAMES];
	static bool	gameStatus[NUM_GAMES];
	static bool	otherGamesFallback;
	static bool ignoreArchiveErrors;
};

inline GameManager::GameResources & GameManager::getNIFResources( const NifModel * nif )
{
	auto	i = nifResourceMap.find( nif );
	if ( i != nifResourceMap.end() ) [[likely]]
		return *(i->second);
	return archives[get_game(nif)];
}

inline GameManager::GameResources & GameManager::getGameResources( const GameMode game )
{
	if ( !( game > OTHER && game < NUM_GAMES ) ) [[unlikely]]
		return archives[OTHER];
	return archives[game];
}

QString GameManager::path( const QString & game )
{
	return path( ModeForString(game) );
}

QStringList GameManager::folders( const QString & game )
{
	return folders( ModeForString(game) );
}

bool GameManager::status( const QString & game )
{
	return status( ModeForString(game) );
}

void GameManager::update_game( const GameMode game, const QString & path )
{
	insert_game( game, path );
}

void GameManager::update_game( const QString & game, const QString & path )
{
	update_game( ModeForString(game), path );
}

void GameManager::update_folders( const GameMode game, const QStringList & list )
{
	insert_folders( game, list );
}

void GameManager::update_status( const GameMode game, bool status )
{
	insert_status( game, status );
}

void GameManager::update_status( const QString & game, bool status )
{
	update_status( ModeForString(game), status );
}

void GameManager::update_other_games_fallback( bool status )
{
	otherGamesFallback = status;
}

void GameManager::update_ignore_archive_errors( bool status )
{
	ignoreArchiveErrors = status;
}

} // end namespace Game

#endif // GAMEMANAGER_H
