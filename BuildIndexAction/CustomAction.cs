using Microsoft.Deployment.WindowsInstaller;
using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace BuildIndexAction
{
    public class CustomActions
    {
        public CustomActions(Session session)
        {
            _session = session;
            _msiAction = ParseMsiAction(session.CustomActionData);
            _installDir = session.CustomActionData["INSTALLLOCATION"];
            _dbFile = Path.Combine(_installDir, "index.sqlite");

            insertDirectory_name = AddParameter(insertDirectoryCommand, "name", System.Data.DbType.AnsiString);
            insertDirectory_parent = AddParameter(insertDirectoryCommand, "parent", System.Data.DbType.Int64);
            insertFile_name = AddParameter(insertFileCommand, "name", System.Data.DbType.AnsiString);
            insertFile_path = AddParameter(insertFileCommand, "path", System.Data.DbType.AnsiString);
            insertFile_directory = AddParameter(insertFileCommand, "directory", System.Data.DbType.Int64);
            insertFile_creationTime = AddParameter(insertFileCommand, "cr_date", System.Data.DbType.Int64);
        }

        private Session _session;
        private MsiAction _msiAction;
        private string _installDir;
        private string _dbFile;
        private static readonly string CreateStatement = "CREATE TABLE Directories ('Id' INTEGER PRIMARY KEY AUTOINCREMENT, 'Name' TEXT NOT NULL, 'ParentId' INTEGER ); CREATE TABLE Files ( 'Id' INTEGER PRIMARY KEY AUTOINCREMENT, 'Name' TEXT NOT NULL, 'Path' TEXT NOT NULL, 'DirectoryId' INTEGER, 'CreationTime' INTEGER, FOREIGN KEY('DirectoryId') REFERENCES 'Directories'('Id') );";
        private static readonly string DeleteStatement = "DELETE FROM Directories; DELETE FROM Files; DELETE FROM sqlite_sequence WHERE name='Directories' OR name='Files'";
        private readonly SQLiteCommand insertDirectoryCommand = new SQLiteCommand("INSERT INTO Directories(Name, ParentId) VALUES($name, $parent); SELECT last_insert_rowid() FROM Directories;");
        private readonly SQLiteParameter insertDirectory_name, insertDirectory_parent;
        private readonly SQLiteCommand insertFileCommand = new SQLiteCommand("INSERT INTO Files(Name, Path, DirectoryId, CreationTime) VALUES($name, $path, $directory, $cr_date);");
        private readonly SQLiteParameter insertFile_name, insertFile_path, insertFile_directory, insertFile_creationTime;

        [CustomAction]
        public static ActionResult BuildIndex(Session session)
        {
            try
            {
                EmbeddedAssembly.Load(string.Format("BuildIndexAction.{0}.SQLite.Interop.dll", Environment.Is64BitProcess ? "x64" : "x86"), "SQLite.Interop.dll");
            }
            catch (Exception e)
            {
                session.Message(InstallMessage.Error, RecordException(e));
                return ActionResult.Failure;
            }

            session.Log("Begin building index");
            var action = new CustomActions(session);
            try
            {
                action.Run();
            }
            catch (Exception e)
            {
                session.Message(InstallMessage.Error, RecordException(e));
                return ActionResult.Failure;
            }

            return ActionResult.Success;
        }

        private static Record RecordException(Exception e)
        {
            Record record = new Record(1);
            record["EXCEPTIONTEXT"] = e.ToString();
            return record;
        }

        private static MsiAction ParseMsiAction(CustomActionData data)
        {
            Func<string, bool> parse = (c) => !String.IsNullOrEmpty(c) && bool.Parse(c);

            MsiAction action = MsiAction.None;

            if (parse(data["FirstInstall"]))
                action |= MsiAction.FirstInstall;
            if (parse(data["Upgrading"]))
                action |= MsiAction.Upgrading;
            if (parse(data["RemovingForUpgrade"]))
                action |= MsiAction.RemovingForUpgrade;
            if (parse(data["Uninstalling"]))
                action |= MsiAction.Uninstalling;
            if (parse(data["Maintenance"]))
                action |= MsiAction.Maintenance;

            return action;
        }

        public static SQLiteParameter AddParameter(SQLiteCommand command, string name, System.Data.DbType type)
        {
            var parameter = command.CreateParameter();
            parameter.DbType = type;
            parameter.ParameterName = name;
            command.Parameters.Add(parameter);
            return parameter;
        }

        private void Run()
        {
            if (_msiAction.HasFlag(MsiAction.RemovingForUpgrade) || _msiAction.HasFlag(MsiAction.Uninstalling))
            {
                // Cleanup
                if (File.Exists(_dbFile))
                    File.Delete(_dbFile);
                if (Directory.Exists(_installDir) && !Directory.EnumerateFileSystemEntries(_installDir).Any())
                    Directory.Delete(_installDir);
                return;
            }

            SQLiteConnection connection = (_msiAction == MsiAction.FirstInstall || !File.Exists(_dbFile)) ? CreateDB() : CleanupDB();
            using (connection)
            {
                // Build index
                using (var transaction = connection.BeginTransaction())
                {
                    insertDirectoryCommand.Connection = connection;
                    insertDirectoryCommand.Transaction = transaction;
                    insertFileCommand.Connection = connection;
                    insertFileCommand.Transaction = transaction;

                    foreach (var childDir in new DirectoryInfo(_installDir).EnumerateDirectories())
                    {
                        IndexDirectory(childDir, null);
                    }
                    transaction.Commit();
                }
            }
        }

        private SQLiteConnection CreateDB()
        {
            SQLiteConnection.CreateFile(_dbFile);
            SQLiteConnection conn = new SQLiteConnection(string.Format("Data Source={0};Version=3;", _dbFile));
            conn.Open();
            using (SQLiteCommand command = new SQLiteCommand(CreateStatement, conn))
            {
                command.ExecuteNonQuery();
            }
            return conn;
        }

        private SQLiteConnection CleanupDB()
        {
            SQLiteConnection conn = new SQLiteConnection(string.Format("Data Source={0};Version=3;", _dbFile));
            conn.Open();
            using (SQLiteCommand command = new SQLiteCommand(DeleteStatement, conn))
            {
                command.ExecuteNonQuery();
            }
            return conn;
        }

        private void IndexDirectory(DirectoryInfo directory, long? parentDir)
        {
            // insert information about this directory
            string categoryName = directory.Name;
            insertDirectory_name.Value = directory.Name;
            insertDirectory_parent.Value = parentDir;
            long directoryId = (long)insertDirectoryCommand.ExecuteScalar();

            foreach (var childDir in directory.EnumerateDirectories())
            {
                IndexDirectory(childDir, directoryId);
            }
            foreach (var file in directory.EnumerateFiles("*.txt"))
            {
                // insert information about this file
                IndexFile(file, directoryId);
            }
        }

        private void IndexFile(FileInfo file, long? parentDir)
        {
            insertFile_directory.Value = parentDir;
            insertFile_name.Value = file.Name;
            insertFile_path.Value = file.FullName;
            insertFile_creationTime.Value = file.CreationTime.ToFileTime();

            insertFileCommand.ExecuteNonQuery();
        }
    }
}
