using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GitSharp;
using GitSharp.Commands;

namespace BranchDatabases
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.IO;
    using System.Reflection;

    namespace DatabaseHelpersExample
    {
        public class GitLocalDb
        {
            private const string ExistsSql = "select 1 from sys.databases where name = '{0}'";
            private const string MasterConnectionString = @"Data Source=(LocalDB)\v11.0;Initial Catalog=master;Integrated Security=True";
            private readonly bool _forceNew;
            public static string DatabaseDirectory = "Data";

            public string ConnectionStringName { get; private set; }
            public string DatabaseName { get; private set; }
            public string OutputFolder { get; private set; }
            public string DatabaseMdfPath { get; private set; }
            public string DatabaseLogPath { get; private set; }
            public string BranchName { get; private set; }

            public GitLocalDb(string prefix, string outputPath = null, bool forceNew = false)
            {
                _forceNew = forceNew;
                BranchName = GetBranchName();
                DatabaseName = string.Join("_", prefix, BranchName);
                OutputFolder = Path.Combine(string.IsNullOrEmpty(outputPath)
                    ? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                    : outputPath, DatabaseDirectory);
                Initialize();
            }

            public IDbConnection OpenConnection()
            {
                return new SqlConnection(ConnectionStringName);
            }

            public void Destroy()
            {
                using (var connection = new SqlConnection(MasterConnectionString))
                {
                    connection.Open();
                    DetachDatabase(connection);
                }
            }

            protected void Initialize()
            {
                var mdfFilename = string.Format("{0}.mdf", DatabaseName);
                DatabaseMdfPath = Path.Combine(OutputFolder, mdfFilename);
                DatabaseLogPath = Path.Combine(OutputFolder, String.Format("{0}_log.ldf", DatabaseName));

                // Create Data Directory If It Doesn't Already Exist.
                if (!Directory.Exists(OutputFolder))
                {
                    Directory.CreateDirectory(OutputFolder);
                }

                // If the database does not already exist, create it.
                using (var connection = new SqlConnection(MasterConnectionString))
                {
                    connection.Open();

                    if (_forceNew || !DatabaseExists(connection))
                    {
                        CreateNewDatabase(connection);
                    }
                    else
                    {
                        ConnectionStringName = string.Format(@"Data Source=(LocalDB)\v11.0;Initial Catalog={0};Integrated Security=True;", DatabaseName);
                    }
                }
            }

            protected void CreateNewDatabase(SqlConnection connection)
            {
                var cmd = connection.CreateCommand();
                Destroy();
                var sql = string.Format(@"if not exists(select * from sys.databases where name = '{0}') CREATE DATABASE {0} ON (NAME = N'{0}', FILENAME = '{1}')", DatabaseName, DatabaseMdfPath);

                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
                // Open newly created, or old database.
                ConnectionStringName = String.Format(@"Data Source=(LocalDB)\v11.0;AttachDBFileName={1};Initial Catalog={0};Integrated Security=True;", DatabaseName, DatabaseMdfPath);
            }

            protected bool DatabaseExists(SqlConnection connection)
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = string.Format(ExistsSql, DatabaseName);
                    using (var reader = command.ExecuteReader())
                    {
                        return reader.HasRows;
                    }
                }
            }

            protected void DetachDatabase(SqlConnection connection)
            {
                try
                {
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = string.Format("ALTER DATABASE {0} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; exec sp_detach_db '{0}'", DatabaseName);
                    cmd.ExecuteNonQuery();
                }
                catch { }
                finally
                {
                    if (File.Exists(DatabaseMdfPath)) File.Delete(DatabaseMdfPath);
                    if (File.Exists(DatabaseLogPath)) File.Delete(DatabaseLogPath);
                }
            }

            protected string GetBranchName()
            {
                // Start the child process.
                var p = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        FileName = "git",
                        Arguments = "rev-parse --abbrev-ref HEAD",
                    }
                };
                p.Start();
                var output = p.StandardOutput.ReadToEnd();

                if (output.Contains("fatal"))
                    throw new Exception("you are not executing in a git directory");

                p.WaitForExit();
                return output.Trim();
            }
        }
    }
}
