using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Data.SQLite;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Policy;
using System.Collections.Specialized;
using System.Data.SqlClient;

namespace WorkerPMS
{
    class Program
    {
        static void Main(string[] args)
        {
            /*
             * This piece of software is designed to be run as a process for the PatientManagementSystem, removing any backend from the frontend application,
             * This manages things like Encryption and Decryption (RSA), Database Data Read
             */
            /*
             * Optimization:
             * Instead of using if(args[0] == "0x0") I'm using switch(args[0]){ case "0x0":}
             * This is faster at runtime because during the compilation if statements don't have pointers assigned to each possible value and each value is compared
             * Whereas each switch case statement has a preassigned pointer to each potential input/query
             * In the case no match is assigned it reverts to the default case
             */
            UTF8Encoding ByteConverter = new UTF8Encoding();  // Converts input (Application Execution Arguements) to UTF8
            switch (args[0])
            {
                
                // 0x = Enc Dec Auth
                // 1x = Database
                // 2x = TCP
                #region Encryption, Decryption, Auth
                case "0x0": // Example execution: WorkerPMS.exe 0x0 dev
                    string encDecTest = Convert.ToBase64String(RSAManager.Encrypt(args[1])); // I've tried any other method and the biggest issue is returning the values as a string, Base64 is the only method viable.
                    Console.Out.WriteLine(encDecTest);
                    /*byte[] encDecByte = Convert.FromBase64String(encDecTest);
                    Console.Out.WriteLine(ByteConverter.GetString(RSAManager.Decrypt(encDecByte)));*/ // This is obsolete, its here from an earlier version
                    break;
                case "0x1":
                    //Execute with WorkerPMS.exe 0x1 (encValue)
                    // This converts our string input into Base64 and then send it to our decryption module, returns it back and converts it back to a std string
                    byte[] decValue = Convert.FromBase64String(args[1]);
                    Console.Out.WriteLine(ByteConverter.GetString(RSAManager.Decrypt(decValue)));
                    break;
                case "0x2":
                    //Executed with WorkerPMS.exe 0x2 (username) (permission to check)
                    //User Sessions are To Be Implemented
                    DatabaseManager.InitializeDatabase();
                    Console.Out.WriteLine(PermissionsManager.SendPermissionRequest(args[1], args[2]));
                    return; 
                #endregion
                #region DB
                case "1x1":
                    if (DatabaseManager.CompareHashData(args[1], args[2], args[3]))
                        Console.Out.WriteLine(PermissionsManager.SendPermissionRequest(args[2], "Auth_Login"));
                    return;
                case "1x2":
                    DatabaseManager.InitializeDatabase();
                    break;
                case "1x3":
                    // Multi Result Query (Search for patients) 
                   // DatabaseManager.CheckUserSession("check", args[args.Length-1], args[args.Length]);
                    DatabaseManager.InitializeDatabase();
                    List<string> strList = new List<string>();
                    // Index 0 = launch mode, index 1 = query string PFLD, index 2 onwards is data to search
                    foreach(string arg in args)
                    {
                        strList.Add(arg);
                    }
                    string[] res = DatabaseManager.GetDataList(strList.ToArray(), "2").ToArray();
                    try
                    {
                        for (int i = 0; i <= res.Length; i++)
                        {
                            Console.WriteLine(res[i]);
                        }
                        
                    }
                    catch(Exception ex)
                    {
                        // It's a stupid issue but for some reason my array.Length extends out of bounds when converted so I'm catching the issue and avoid it, it works so I'm going to leave it alot
                        // Technically I can use a foreach but for now I'll keep it as this, that change may come later.
                    }
                    break;
                case "1x4":
                    //Single Result Query (Often used to grab one string from db for example username
                    //WorkerPMS.exe 1x4 1u (username)
                    DatabaseManager.InitializeDatabase();
                    Console.Out.WriteLine(DatabaseManager.GetData(args[2], args[1]));
                    return;
                case "1x5":
                    // Insert Data req
                    bool dataInsert = DatabaseManager.InsertData(args[1],args[2],args[3]);
                    if (dataInsert)
                        Console.Out.WriteLine("Success");
                    else
                        Console.Out.WriteLine("Failed");
                    break;
                case "1xU":
                    //Update Data req
                    bool dataUpdate = DatabaseManager.UpdateData(args[1], args[2], args[3]);
                    if (dataUpdate)
                        Console.WriteLine("Success");
                    else
                         Console.Out.WriteLine("Failed");
                    break;

                #endregion
                #region TCP
                case "2x1":
                    //TCPClient
                    break;
                case "2x2":
                    //TCPServer, initalized before the client
                    break;
                #endregion
            }
        }
        #region Local
        class RSAManager
        {
            public static byte[] Decrypt(byte[] value)
            {
                try
                {
                    var parameters = new CspParameters
                    {
                        KeyContainerName = "workerPMSKey",
                    };
                    using var RSA = new RSACryptoServiceProvider(parameters)
                    {
                        PersistKeyInCsp = true
                    };
                    return RSA.Decrypt(value, false);
                }
                catch (CryptographicException ex)
                {
                    Console.WriteLine(ex);
                    return null;
                }
            }
            public static byte[] Encrypt(string value)
            {
                try
                {
                    UTF8Encoding ByteConverter = new UTF8Encoding();
                    var parameters = new CspParameters
                    {
                        KeyContainerName = "workerPMSKey",
                    };

                    var RSA = new RSACryptoServiceProvider(parameters)
                    {
                        PersistKeyInCsp = true
                    };
                    var RSAEnc = RSA.Encrypt(ByteConverter.GetBytes(value), false);
                    return RSAEnc;
                }
                catch (CryptographicException ex)
                {
                    return null;
                }
            }
        }
        class DatabaseManager
        {
            public static void InitializeDatabase()
            {
                FileInfo fi = new FileInfo(@"Worker\pms.db");
                if (!File.Exists(@"Worker\pms.db") ^ (fi.Length == 0))
                {
                    SQLiteConnection.CreateFile(@"Worker\pms.db");
                    using (var con = new SQLiteConnection("Data Source=pms.db"))
                    {
                        con.Open();

                        var patientTableUnsync = con.CreateCommand();
                        patientTableUnsync.CommandText = @"CREATE TABLE 'unsyncPatients' ('ExID' INTEGER NOT NULL AUTO_INCREMENT UNIQUE, 'PatientID' INTEGER NOT NULL,'FName' TEXT NOT NULL,'LName' TEXT NOT NULL,'DateOfBirth' TEXT NOT NULL,'HouseNo.' TEXT NOT NULL,'AddressLine1' TEXT NOT NULL,'Postcode' TEXT NOT NULL,'PhoneNumber' TEXT NOT NULL,'EmailAddress' TEXT NOT NULL, 'MODE' TEXT NOT NULL, PRIMARY KEY('ExID'));";
                        patientTableUnsync.ExecuteNonQuery();

                        var patientTableSync = con.CreateCommand();
                        patientTableSync.CommandText = "CREATE TABLE 'Patients' ('PatientID' INTEGER NOT NULL UNIQUE,'FName' TEXT NOT NULL,'LName' TEXT NOT NULL,'DateOfBirth' TEXT NOT NULL,'HouseNo.' TEXT NOT NULL,'AddressLine1' TEXT NOT NULL,'Postcode' TEXT NOT NULL,'PhoneNumber' TEXT NOT NULL,'EmailAddress' TEXT NOT NULL, PRIMARY KEY('PatientID'));";
                        patientTableSync.ExecuteNonQuery();

                        var usrTable = con.CreateCommand();
                        usrTable.CommandText = @"CREATE TABLE 'User' ('username'  TEXT NOT NULL, 'password'  TEXT NOT NULL, 'id'    INTEGER UNIQUE,'userGroup'  TEXT NOT NULL,'session'  TEXT NOT NULL, PRIMARY KEY('id'));";
                        usrTable.ExecuteNonQuery();

                        var unsyncusrTable = con.CreateCommand();
                        unsyncusrTable.CommandText = @"CREATE TABLE 'unsyncUser' ('ExID' INTEGER NOT NULL AUTO_INCREMENT UNIQUE, 'username'  TEXT NOT NULL, 'password'  TEXT NOT NULL, 'id'    INTEGER ,'userGroup'  TEXT NOT NULL,'session'  TEXT NOT NULL, 'MODE' TEXT NOT NULL, PRIMARY KEY('ExID'));";
                        unsyncusrTable.ExecuteNonQuery();
                        
                        var permissions = con.CreateCommand();
                        permissions.CommandText = @"CREATE TABLE 'Permissions' ('GroupID' TEXT, 'Auth_Login' TEXT,'Staff_Patient_Manage' TEXT, 'Staff_Patient_Search' TEXT, 'Staff_Patient_Update' TEXT, 'Staff_Patient_Delete' TEXT, 'Staff_Patient_Add' TEXT, 'Staff_Appointment_Add' TEXT, 'Staff_Appointment_Delete' TEXT, 'Staff_Appointment_Update' TEXT, 'Staff_Appointment_Search' TEXT, PRIMARY KEY('GroupID'));";
                        permissions.ExecuteNonQuery();

                        var unsyncAppointments = con.CreateCommand();
                        unsyncAppointments.CommandText = @"CREATE TABLE 'unsyncAppointments' ('ExID' INTEGER NOT NULL AUTO_INCREMENT UNIQUE, 'AppointmentID' INTEGER NOT NULL ,'AppointmentState' TEXT,'AppointmentDate' TEXT,'AppointmentTimeFrom' TEXT,'AppointmentTimeTo' TEXT,'AppointmentStaffID' TEXT,'AppointmentPatientID' TEXT, 'AppointmentType' TEXT, 'AppointmentCost' TEXT, 'AppointmentNotes' TEXT, 'AppointmentComplete' TEXT, 'AppointmentPaid' TEXT, 'MODE' TEXT NOT NULL, PRIMARY KEY('ExID'));";
                        unsyncAppointments.ExecuteNonQuery();


                        var appointments = con.CreateCommand();
                        appointments.CommandText = @"CREATE TABLE 'Appointments' ('AppointmentID' INTEGER NOT NULL UNIQUE,'AppointmentState' TEXT,'AppointmentDate' TEXT,'AppointmentTimeFrom' TEXT,'AppointmentTimeTo' TEXT,'AppointmentStaffID' TEXT,'AppointmentPatientID' TEXT, 'AppointmentType' TEXT, 'AppointmentCost' TEXT, 'AppointmentNotes' TEXT, 'AppointmentComplete' TEXT, 'AppointmentPaid' TEXT, PRIMARY KEY('AppointmentID'));";
                        appointments.ExecuteNonQuery();

                        var permAdminInsert = con.CreateCommand();
                        permAdminInsert.CommandText = @"INSERT INTO Permissions VALUES('Admin', 'Admin','TRUE','TRUE','TRUE','TRUE','TRUE','TRUE','TRUE','TRUE','TRUE');";
                        permAdminInsert.ExecuteNonQuery();
                        
                        var permStaffInsert = con.CreateCommand();
                        permStaffInsert.CommandText = @"INSERT INTO Permissions VALUES('Staff', 'Staff','TRUE','TRUE','TRUE','TRUE','TRUE','TRUE','TRUE','TRUE','TRUE');";
                        permStaffInsert.ExecuteNonQuery();

                        var permPatientInsert = con.CreateCommand();
                        permPatientInsert.CommandText = @"INSERT INTO Permissions VALUES('Patient', 'Patient','FALSE','FALSE','FALSE','FALSE','FALSE','FALSE','FALSE','FALSE','FALSE');";
                        permPatientInsert.ExecuteNonQuery();

                        var userCom = con.CreateCommand();
                        userCom.CommandText = $"INSERT INTO 'unsyncUser' VALUES('devTemp', '{Convert.ToBase64String(RSAManager.Encrypt("test"))}','2','Staff', random())"; // Remove after add user command is made
                        userCom.ExecuteNonQuery();
                        // TODO: Add the Permissions Table and Insert Default Permission Values. (Commands in place, just need full db structure)
                        con.Close();
                    }
                }
            }
            #region Get Data
            public static string GetData(string comparisonValue, string dataLocation)
            {
                /*
                 * The Comparison Value is a preselected value used to get data from the database
                 * The dataLocation int defines which table to get the data from, whether it be the user table or the patient table.
                 * dataLocation IDs:
                 * 
                 * Why I started with Index Value 1: Because index value 0 is confusing and an arse to debug when you forget it all starts with 0
                 * 
                 * 1 - User Table
                 * 2 - Patient Table
                 * 3 - Sync Table (haven't quite decided how this table is going to work)
                 * 4 - Permissions Table
                 * 
                 * How each table will be requested:
                 * 1 - Login Request, searches for the username of an account and gets the password hash returning it to the main application (Console.Out.WriteLine(GetData(%usernameValue%, 1);
                 * 
                 */
                string table = "null";
                string where = "null";
                string valueToReturn = "null";
                // Again a switch case may be put here later but for now it works as it is.
                // Using the older dataLocation, still works but will switch it

                if (dataLocation == "1")
                {
                    table = "User";
                    where = "username";
                    valueToReturn = "password";
                }
                else if (dataLocation == "1a") // Represents auth
                {
                    table = "User";
                    where = "username";
                    valueToReturn = "session";
                }
                else if (dataLocation == "1g") // Represents Group
                {
                    table = "User";
                    where = "username";
                    valueToReturn = "userGroup";
                }
                else if (dataLocation == "1u")
                {
                    table = "User";
                    where = "username";
                    valueToReturn = "Name";
                }
                else if (dataLocation == "2")
                {
                    table = "Patients";
                    where = "id";
                    valueToReturn = "*"; // Will have to adapt data stream to access current data
                }
                using (var con = new SQLiteConnection("Data Source=pms.db"))
                {
                    con.Open();
                    var syncCom = con.CreateCommand();
                    syncCom.CommandText = $"SELECT {valueToReturn} FROM {table} WHERE {where}='@compValue';";
                    syncCom.Parameters.AddWithValue("@compValue", comparisonValue);
                    syncCom.CommandType = System.Data.CommandType.Text;
                    var res = syncCom.ExecuteScalar();
                    if (res != null)
                        return res.ToString();
                    else
                    {
                        var unsyncCom = con.CreateCommand();
                        unsyncCom.CommandText = $"SELECT {valueToReturn} FROM unsync{table} WHERE {where}='@compValue';";
                        unsyncCom.Parameters.AddWithValue("@compValue", comparisonValue);
                        unsyncCom.CommandType = System.Data.CommandType.Text;
                        //SQLiteDataReader unsyncReader = syncCom
                        string ures = unsyncCom.ExecuteScalar().ToString();
                        if (ures != null)
                            return ures;
                        else
                            return "null";
                    }
                }
            }
            /// <summary>
            /// Built for Permissions Request Allowing Definition of valueToRetun
            /// </summary>
            /// <param name="dataLocation"></param>
            /// <param name="comparisonValue"></param>
            /// <param name="valueToReturn"></param>
            /// <returns></returns>
            public static string GetData(string dataLocation, string comparisonValue, string valueToReturn)
            {
                /*
                 * The Comparison Value is a preselected value used to get data from the database
                 * The dataLocation int defines which table to get the data from, whether it be the user table or the patient table.
                 * dataLocation IDs:
                 * 
                 * Why I started with Index Value 1: Because index value 0 is confusing and an arse to debug when you forget it all starts with 0
                 * 
                 * 1 - User Table
                 * 2 - Patient Table
                 * 3 - Sync Table (haven't quite decided how this table is going to work)
                 * 
                 * 
                 * How each table will be requested:
                 * 1 - Login Request, searches for the username of an account and gets the password hash returning it to the main application (Console.Out.WriteLine(GetData(%usernameValue%, 1);
                 * 
                 */
                string table = "null";
                string where = "null";
                // Again a switch case may be put here later but for now it works as it is.
                // Using the older dataLocation, still works but will switch it

                if (dataLocation == "PERM_REQ")
                {
                    table = "Permissions";
                    where = "GroupID";
                }
                using (var con = new SQLiteConnection("Data Source=pms.db"))
                {
                    con.Open();
                    var syncCom = con.CreateCommand();
                    syncCom.CommandText = $"SELECT {valueToReturn} FROM {table} WHERE {where}='@compValue';";
                    syncCom.Parameters.AddWithValue("@compValue", comparisonValue);
                    syncCom.CommandType = System.Data.CommandType.Text;
                    var res = syncCom.ExecuteScalar();
                    if (res != null)
                        return res.ToString();
                    else
                    { 
                        // This is being left in since if I want to expand to an Admin Permissions Manager then I'll have to Sync Permissions between clients, this will ensure that the permission is updated accordingly locally
                        var unsyncCom = con.CreateCommand();
                        unsyncCom.CommandText = $"SELECT {valueToReturn} FROM unsync{table} WHERE {where}='@compValue';";
                        unsyncCom.Parameters.AddWithValue("@compValue", comparisonValue);
                        unsyncCom.CommandType = System.Data.CommandType.Text;
                        //SQLiteDataReader unsyncReader = syncCom
                        string ures = unsyncCom.ExecuteScalar().ToString();
                        if (ures != null)
                            return ures;
                        else
                            return "null";
                    }
                }
        }
            public static List<string> GetDataList(string[] values, string dataLocation)
            {
                /*
                 * The Comparison Value is a preselected value used to get data from the database
                 * The dataLocation int defines which table to get the data from, whether it be the user table or the patient table.
                 * dataLocation IDs:
                 * 
                 * Why I started with Index Value 1: Because index value 0 is confusing and an arse to debug when you forget it all starts with 0
                 * 
                 * 1 - User Table
                 * 2 - Patient Table
                 * 3 - Sync Table (haven't quite decided how this table is going to work)
                 * 
                 * How each table will be requested:
                 * 1 - Login Request, searches for the username of an account and gets the password hash returning it to the main application (Console.Out.WriteLine(GetData(%usernameValue%, 1);
                 * 
                 */
                using (var con = new SQLiteConnection("Data Source=pms.db"))
                {
                    con.Open();
                    var com = con.CreateCommand();
                    var unsyncCom = con.CreateCommand();
                    string loc = "";
                    com.CommandType = System.Data.CommandType.Text;
                    unsyncCom.CommandType = System.Data.CommandType.Text;
                    string searchCriteria = "";
                    StringDictionary dbPatientCriteria = new StringDictionary();
                    dbPatientCriteria.Add("P", "PatientID");
                    dbPatientCriteria.Add("F", "FName");
                    dbPatientCriteria.Add("L", "LName");
                    dbPatientCriteria.Add("D", "DateOfBirth");
                    dbPatientCriteria.Add("p", "Postcode");
                    dbPatientCriteria.Add("E", "EmailAddress");

                    StringDictionary dbAppointmentSearchCriteria = new StringDictionary();
                    dbAppointmentSearchCriteria.Add("D", "AppointmentDate");
                    dbAppointmentSearchCriteria.Add("I", "AppointmentID");
                    dbAppointmentSearchCriteria.Add("s", "AppointmentState");
                    dbAppointmentSearchCriteria.Add("S", "AppointmentStaffID");
                    dbAppointmentSearchCriteria.Add("P", "AppointmentPatientID");
                    dbAppointmentSearchCriteria.Add("t", "AppointmentTimeFrom");
                    dbAppointmentSearchCriteria.Add("T", "AppointmentTimeTo");
                    dbAppointmentSearchCriteria.Add("C", "AppointmentCost");
                    dbAppointmentSearchCriteria.Add("a", "AppointmentCompleted");
                    #region OldCode
                    /*
                     * These if statements can be optimized technically but the actual optimization would only be a few ms, I'll switch this out later if i really need to.
                     * 
                     * 
                     */

                    // This next commented out section of code in the / * * / section displays what I had in place, this was swapped out in order to repair the SQL Inject Vuln
                    // It looks nicer aswell the new section

                    /*if (dataLocation == "1")
                    {
                        loc = "User";
                    }
                    else if (dataLocation == "2")
                    {
                        loc = "Patients";
                        if (values[1].ToString().Length == 1)
                        {
                            searchCriteria = $"WHERE {dbPatientCriteria[values[1]]}='{values[2]}';";

                        }
                        else if (values[1].ToString().Length == 2)
                        {
                            searchCriteria = $"WHERE {dbPatientCriteria[values[1].ToString().Substring(0, 1)]} = '{values[2]}' AND {dbPatientCriteria[values[1].ToString().Substring(1, 1)]} = '{values[3]}';";

                        }
                        else if (values[1].ToString().Length == 3)
                        {

                            searchCriteria = $"WHERE {dbPatientCriteria[values[1].Substring(0, 1)]}='{values[2]}' AND {dbPatientCriteria[values[1].Substring(1, 1)]}='{values[3]}' AND {dbPatientCriteria[values[1].Substring(2, 1)]}='{values[4]}';";

                        }
                        else if (values[1].ToString().Length == 4)
                        {
                            searchCriteria = $"WHERE {dbPatientCriteria[values[1].Substring(1, 1)]}='{values[2]}' AND {dbPatientCriteria[values[1].Substring(2, 1)]}='{values[3]}' AND {dbPatientCriteria[values[1].Substring(3, 1)]}='{values[4]}' AND {dbPatientCriteria[values[1].Substring(4, 1)]}='{values[5]}';";
                        }
                        else if (values[1].ToString().Length == 5)
                        {
                            searchCriteria = $"WHERE {dbPatientCriteria[values[1].Substring(1, 1)]}='{values[2]}' AND {dbPatientCriteria[values[1].Substring(2, 1)]}='{values[3]}' AND {dbPatientCriteria[values[1].Substring(3, 1)]}='{values[4]}' AND {dbPatientCriteria[values[1].Substring(4, 1)]}='{values[5]}' AND {dbPatientCriteria[values[1].Substring(5, 1)]}='{values[6]}';";
                        }
                        else if (values[1].ToString().Length == 6)
                        {
                            searchCriteria = $"WHERE {dbPatientCriteria[values[1].Substring(1, 1)]}='{values[2]}' AND {dbPatientCriteria[values[1].Substring(2, 1)]}='{values[3]}' AND {dbPatientCriteria[values[1].Substring(3, 1)]}='{values[4]}' AND {dbPatientCriteria[values[1].Substring(4, 1)]}='{values[5]}' AND {dbPatientCriteria[values[1].Substring(5, 1)]}='{values[6]}' AND {dbPatientCriteria[values[1].Substring(6, 1)]}='{values[7]}';";
                        }
                    }
                    else if (dataLocation == "3")
                    {
                        loc = "Appointments";
                        if (values[1].ToString().Length == 1)
                        {
                            searchCriteria = $"WHERE {dbAppointmentSearchCriteria[values[1]]}='{values[2]}';";

                        }
                        else if (values[1].ToString().Length == 2)
                        {
                            searchCriteria = $"WHERE {dbAppointmentSearchCriteria[values[1].ToString().Substring(0, 1)]} = '{values[2]}' AND {dbAppointmentSearchCriteria[values[1].ToString().Substring(1, 1)]} = '{values[3]}';";

                        }
                        else if (values[1].ToString().Length == 3)
                        {

                            searchCriteria = $"WHERE {dbAppointmentSearchCriteria[values[1].Substring(0, 1)]}='{values[2]}' AND {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='{values[3]}' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='{values[4]}';";

                        }
                        else if (values[1].ToString().Length == 4)
                        {
                            searchCriteria = $"WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='{values[2]}' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='{values[3]}' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='{values[4]}' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='{values[5]}';";
                        }
                        else if (values[1].ToString().Length == 5)
                        {
                            searchCriteria = $"WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='{values[2]}' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='{values[3]}' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='{values[4]}' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='{values[5]}' AND {dbAppointmentSearchCriteria[values[1].Substring(5, 1)]}='{values[6]}';";
                        }
                        else if (values[1].ToString().Length == 6)
                        {
                            searchCriteria = $"WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='{values[2]}' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='{values[3]}' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='{values[4]}' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='{values[5]}' AND {dbAppointmentSearchCriteria[values[1].Substring(5, 1)]}='{values[6]}' AND {dbAppointmentSearchCriteria[values[1].Substring(6, 1)]}='{values[7]}';";
                        }
                        else if (values[1].ToString().Length == 7)
                        {
                            searchCriteria = $"WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='{values[2]}' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='{values[3]}' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='{values[4]}' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='{values[5]}' AND {dbAppointmentSearchCriteria[values[1].Substring(5, 1)]}='{values[6]}' AND {dbAppointmentSearchCriteria[values[1].Substring(6, 1)]}='{values[7]}' AND {dbAppointmentSearchCriteria[values[1].Substring(7, 1)]}='{values[8]}';";
                        }
                        else if (values[1].ToString().Length == 8)
                        {
                            searchCriteria = $"WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='{values[2]}' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='{values[3]}' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='{values[4]}' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='{values[5]}' AND {dbAppointmentSearchCriteria[values[1].Substring(5, 1)]}='{values[6]}' AND {dbAppointmentSearchCriteria[values[1].Substring(6, 1)]}='{values[7]}' AND {dbAppointmentSearchCriteria[values[1].Substring(7, 1)]}='{values[8]}' AND {dbAppointmentSearchCriteria[values[1].Substring(8, 1)]}='{values[9]}';";
                        }
                        else if (values[1].ToString().Length == 9)
                        {
                            searchCriteria = $"WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='{values[2]}' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='{values[3]}' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='{values[4]}' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='{values[5]}' AND {dbAppointmentSearchCriteria[values[1].Substring(5, 1)]}='{values[6]}' AND {dbAppointmentSearchCriteria[values[1].Substring(6, 1)]}='{values[7]}' AND {dbAppointmentSearchCriteria[values[1].Substring(7, 1)]}='{values[8]}' AND {dbAppointmentSearchCriteria[values[1].Substring(8, 1)]}='{values[9]}' AND {dbAppointmentSearchCriteria[values[1].Substring(9, 1)]}='{values[10]}';";
                        }

                    }
                    */
                    #endregion
                    if (dataLocation == "1")
                    {
                        loc = "User";
                    }
                    else if (dataLocation == "2")
                    {
                        loc = "Patients";
                        if(values[1].ToString().Length == 1)
                        {
                            com.CommandText = $"SELECT * FROM '{loc}' WHERE {dbPatientCriteria[values[1].Substring(1,1)]}='@val1';";
                            com.Parameters.AddWithValue("@val1", values[2]);
                            unsyncCom.CommandText = $"SELECT * FROM '{loc}' WHERE {dbPatientCriteria[values[1].Substring(1, 1)]}='@val1';";
                            unsyncCom.Parameters.AddWithValue("@val1", values[2]);
                        }
                        else if (values[1].ToString().Length == 2)
                        {
                            com.CommandText = $"SELECT * FROM '{loc}' WHERE {dbPatientCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbPatientCriteria[values[1].Substring(2, 1)]}='@val2';";
                            com.Parameters.AddWithValue("@val1", values[2]);
                            com.Parameters.AddWithValue("@val2", values[3]);
                            unsyncCom.CommandText = $"SELECT * FROM '{loc}' WHERE {dbPatientCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbPatientCriteria[values[1].Substring(2, 1)]}='@val2';";
                            unsyncCom.Parameters.AddWithValue("@val1", values[2]);
                            unsyncCom.Parameters.AddWithValue("@val2", values[3]);
                        }
                        else if (values[1].ToString().Length == 3)
                        {
                            com.CommandText = $"SELECT * FROM '{loc}' WHERE {dbPatientCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbPatientCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbPatientCriteria[values[1].Substring(3, 1)]}='@val3';";
                            com.Parameters.AddWithValue("@val1", values[2]);
                            com.Parameters.AddWithValue("@val2", values[3]);
                            com.Parameters.AddWithValue("@val3", values[4]);
                            unsyncCom.CommandText = $"SELECT * FROM '{loc}' WHERE {dbPatientCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbPatientCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbPatientCriteria[values[1].Substring(3, 1)]}='@val3';";
                            unsyncCom.Parameters.AddWithValue("@val1", values[2]);
                            unsyncCom.Parameters.AddWithValue("@val2", values[3]);
                            unsyncCom.Parameters.AddWithValue("@val3", values[4]);
                        }
                        else if (values[1].ToString().Length == 4)
                        {
                            com.CommandText = $"SELECT * FROM '{loc}' WHERE {dbPatientCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbPatientCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbPatientCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbPatientCriteria[values[1].Substring(4, 1)]}='@val4';";
                            com.Parameters.AddWithValue("@val1", values[2]);
                            com.Parameters.AddWithValue("@val2", values[3]);
                            com.Parameters.AddWithValue("@val3", values[4]);
                            com.Parameters.AddWithValue("@val4", values[5]);
                            unsyncCom.CommandText = $"SELECT * FROM '{loc}' WHERE {dbPatientCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbPatientCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbPatientCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbPatientCriteria[values[1].Substring(4, 1)]}='@val4';";
                            unsyncCom.Parameters.AddWithValue("@val1", values[2]);
                            unsyncCom.Parameters.AddWithValue("@val2", values[3]);
                            unsyncCom.Parameters.AddWithValue("@val3", values[4]);
                            unsyncCom.Parameters.AddWithValue("@val4", values[5]);
                        }
                        else if (values[1].ToString().Length == 5)
                        {
                            com.CommandText = $"SELECT * FROM '{loc}' WHERE {dbPatientCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbPatientCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbPatientCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbPatientCriteria[values[1].Substring(4, 1)]}='@val4' AND {dbPatientCriteria[values[1].Substring(5, 1)]}='@val5';";
                            com.Parameters.AddWithValue("@val1", values[2]);
                            com.Parameters.AddWithValue("@val2", values[3]);
                            com.Parameters.AddWithValue("@val3", values[4]);
                            com.Parameters.AddWithValue("@val4", values[5]);
                            com.Parameters.AddWithValue("@val5", values[6]);
                            unsyncCom.CommandText = $"SELECT * FROM '{loc}' WHERE {dbPatientCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbPatientCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbPatientCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbPatientCriteria[values[1].Substring(4, 1)]}='@val4' AND {dbPatientCriteria[values[1].Substring(5, 1)]}='@val5';";
                            unsyncCom.Parameters.AddWithValue("@val1", values[2]);
                            unsyncCom.Parameters.AddWithValue("@val2", values[3]);
                            unsyncCom.Parameters.AddWithValue("@val3", values[4]);
                            unsyncCom.Parameters.AddWithValue("@val4", values[5]);
                            unsyncCom.Parameters.AddWithValue("@val5", values[6]);

                        }
                        else if (values[1].ToString().Length == 6)
                        {
                            com.CommandText = $"SELECT * FROM '{loc}' WHERE {dbPatientCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbPatientCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbPatientCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbPatientCriteria[values[1].Substring(4, 1)]}='@val4' AND {dbPatientCriteria[values[1].Substring(5, 1)]}='@val5' AND AND {dbPatientCriteria[values[1].Substring(6, 1)]}='@val6';";
                            com.Parameters.AddWithValue("@val1", values[2]);
                            com.Parameters.AddWithValue("@val2", values[3]);
                            com.Parameters.AddWithValue("@val3", values[4]);
                            com.Parameters.AddWithValue("@val4", values[5]);
                            com.Parameters.AddWithValue("@val5", values[6]);
                            com.Parameters.AddWithValue("@val6", values[7]);
                            unsyncCom.CommandText = $"SELECT * FROM '{loc}' WHERE {dbPatientCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbPatientCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbPatientCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbPatientCriteria[values[1].Substring(4, 1)]}='@val4' AND {dbPatientCriteria[values[1].Substring(5, 1)]}='@val5' AND AND {dbPatientCriteria[values[1].Substring(6, 1)]}='@val6';";
                            unsyncCom.Parameters.AddWithValue("@val1", values[2]);
                            unsyncCom.Parameters.AddWithValue("@val2", values[3]);
                            unsyncCom.Parameters.AddWithValue("@val3", values[4]);
                            unsyncCom.Parameters.AddWithValue("@val4", values[5]);
                            unsyncCom.Parameters.AddWithValue("@val5", values[6]);
                            unsyncCom.Parameters.AddWithValue("@val6", values[7]);
                        }
                    }
                    else if (dataLocation == "3")
                    {
                        loc = "Appointment";
                        if (values[1].ToString().Length == 1)
                        {
                            com.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1';";
                            com.Parameters.AddWithValue("@val1", values[2]);
                            unsyncCom.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1';";
                            unsyncCom.Parameters.AddWithValue("@val1", values[2]);
                        }
                        else if (values[1].ToString().Length == 2)
                        {
                            com.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='@val2';";
                            com.Parameters.AddWithValue("@val1", values[2]);
                            com.Parameters.AddWithValue("@val2", values[3]);
                            unsyncCom.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='@val2';";
                            unsyncCom.Parameters.AddWithValue("@val1", values[2]);
                            unsyncCom.Parameters.AddWithValue("@val2", values[3]);
                        }
                        else if (values[1].ToString().Length == 3)
                        {
                            com.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='@val3';";
                            com.Parameters.AddWithValue("@val1", values[2]);
                            com.Parameters.AddWithValue("@val2", values[3]);
                            com.Parameters.AddWithValue("@val3", values[4]);
                            unsyncCom.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='@val3';";
                            unsyncCom.Parameters.AddWithValue("@val1", values[2]);
                            unsyncCom.Parameters.AddWithValue("@val2", values[3]);
                            unsyncCom.Parameters.AddWithValue("@val3", values[4]);
                        }
                        else if (values[1].ToString().Length == 4)
                        {
                            com.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='@val4';";
                            com.Parameters.AddWithValue("@val1", values[2]);
                            com.Parameters.AddWithValue("@val2", values[3]);
                            com.Parameters.AddWithValue("@val3", values[4]);
                            com.Parameters.AddWithValue("@val4", values[5]);
                            unsyncCom.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='@val4';";
                            unsyncCom.Parameters.AddWithValue("@val1", values[2]);
                            unsyncCom.Parameters.AddWithValue("@val2", values[3]);
                            unsyncCom.Parameters.AddWithValue("@val3", values[4]);
                            unsyncCom.Parameters.AddWithValue("@val4", values[5]);
                        }
                        else if (values[1].ToString().Length == 5)
                        {
                            com.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='@val4' AND {dbAppointmentSearchCriteria[values[1].Substring(5, 1)]}='@val5';";
                            com.Parameters.AddWithValue("@val1", values[2]);
                            com.Parameters.AddWithValue("@val2", values[3]);
                            com.Parameters.AddWithValue("@val3", values[4]);
                            com.Parameters.AddWithValue("@val4", values[5]);
                            com.Parameters.AddWithValue("@val5", values[6]);
                            unsyncCom.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='@val4' AND {dbAppointmentSearchCriteria[values[1].Substring(5, 1)]}='@val5';";
                            unsyncCom.Parameters.AddWithValue("@val1", values[2]);
                            unsyncCom.Parameters.AddWithValue("@val2", values[3]);
                            unsyncCom.Parameters.AddWithValue("@val3", values[4]);
                            unsyncCom.Parameters.AddWithValue("@val4", values[5]);
                            unsyncCom.Parameters.AddWithValue("@val5", values[6]);

                        }
                        else if (values[1].ToString().Length == 6)
                        {
                            com.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='@val4' AND {dbAppointmentSearchCriteria[values[1].Substring(5, 1)]}='@val5' AND {dbAppointmentSearchCriteria[values[1].Substring(6, 1)]}='@val6';";
                            com.Parameters.AddWithValue("@val1", values[2]);
                            com.Parameters.AddWithValue("@val2", values[3]);
                            com.Parameters.AddWithValue("@val3", values[4]);
                            com.Parameters.AddWithValue("@val4", values[5]);
                            com.Parameters.AddWithValue("@val5", values[6]);
                            com.Parameters.AddWithValue("@val6", values[7]);
                            unsyncCom.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='@val4' AND {dbAppointmentSearchCriteria[values[1].Substring(5, 1)]}='@val5' AND {dbAppointmentSearchCriteria[values[1].Substring(6, 1)]}='@val6';";
                            unsyncCom.Parameters.AddWithValue("@val1", values[2]);
                            unsyncCom.Parameters.AddWithValue("@val2", values[3]);
                            unsyncCom.Parameters.AddWithValue("@val3", values[4]);
                            unsyncCom.Parameters.AddWithValue("@val4", values[5]);
                            unsyncCom.Parameters.AddWithValue("@val5", values[6]);
                            unsyncCom.Parameters.AddWithValue("@val6", values[7]);
                        }
                        else if (values[1].ToString().Length == 7)
                        {
                            com.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='@val4' AND {dbAppointmentSearchCriteria[values[1].Substring(5, 1)]}='@val5' AND {dbAppointmentSearchCriteria[values[1].Substring(6, 1)]}='@val6' AND {dbAppointmentSearchCriteria[values[1].Substring(7, 1)]}='@val7';";
                            com.Parameters.AddWithValue("@val1", values[2]);
                            com.Parameters.AddWithValue("@val2", values[3]);
                            com.Parameters.AddWithValue("@val3", values[4]);
                            com.Parameters.AddWithValue("@val4", values[5]);
                            com.Parameters.AddWithValue("@val5", values[6]);
                            com.Parameters.AddWithValue("@val6", values[7]);
                            com.Parameters.AddWithValue("@val7", values[8]);
                            unsyncCom.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='@val4' AND {dbAppointmentSearchCriteria[values[1].Substring(5, 1)]}='@val5' AND {dbAppointmentSearchCriteria[values[1].Substring(6, 1)]}='@val6' AND {dbAppointmentSearchCriteria[values[1].Substring(7, 1)]}='@val7';";
                            unsyncCom.Parameters.AddWithValue("@val1", values[2]);
                            unsyncCom.Parameters.AddWithValue("@val2", values[3]);
                            unsyncCom.Parameters.AddWithValue("@val3", values[4]);
                            unsyncCom.Parameters.AddWithValue("@val4", values[5]);
                            unsyncCom.Parameters.AddWithValue("@val5", values[6]);
                            unsyncCom.Parameters.AddWithValue("@val6", values[7]);
                            unsyncCom.Parameters.AddWithValue("@val7", values[8]);
                        }
                        else if (values[1].ToString().Length == 8)
                        {
                            com.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='@val4' AND {dbAppointmentSearchCriteria[values[1].Substring(5, 1)]}='@val5' AND {dbAppointmentSearchCriteria[values[1].Substring(6, 1)]}='@val6' AND {dbAppointmentSearchCriteria[values[1].Substring(7, 1)]}='@val7' AND {dbAppointmentSearchCriteria[values[1].Substring(8, 1)]}='@val8';";
                            com.Parameters.AddWithValue("@val1", values[2]);
                            com.Parameters.AddWithValue("@val2", values[3]);
                            com.Parameters.AddWithValue("@val3", values[4]);
                            com.Parameters.AddWithValue("@val4", values[5]);
                            com.Parameters.AddWithValue("@val5", values[6]);
                            com.Parameters.AddWithValue("@val6", values[7]);
                            com.Parameters.AddWithValue("@val7", values[8]);
                            com.Parameters.AddWithValue("@val8", values[9]);
                            unsyncCom.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='@val4' AND {dbAppointmentSearchCriteria[values[1].Substring(5, 1)]}='@val5' AND {dbAppointmentSearchCriteria[values[1].Substring(6, 1)]}='@val6' AND {dbAppointmentSearchCriteria[values[1].Substring(7, 1)]}='@val7' AND {dbAppointmentSearchCriteria[values[1].Substring(8, 1)]}='@val8';";
                            unsyncCom.Parameters.AddWithValue("@val1", values[2]);
                            unsyncCom.Parameters.AddWithValue("@val2", values[3]);
                            unsyncCom.Parameters.AddWithValue("@val3", values[4]);
                            unsyncCom.Parameters.AddWithValue("@val4", values[5]);
                            unsyncCom.Parameters.AddWithValue("@val5", values[6]);
                            unsyncCom.Parameters.AddWithValue("@val6", values[7]);
                            unsyncCom.Parameters.AddWithValue("@val7", values[8]);
                            unsyncCom.Parameters.AddWithValue("@val8", values[9]);
                        }
                        else if (values[1].ToString().Length == 9)
                        {
                            com.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='@val4' AND {dbAppointmentSearchCriteria[values[1].Substring(5, 1)]}='@val5' AND {dbAppointmentSearchCriteria[values[1].Substring(6, 1)]}='@val6' AND {dbAppointmentSearchCriteria[values[1].Substring(7, 1)]}='@val7' AND {dbAppointmentSearchCriteria[values[1].Substring(8, 1)]}='@val8' AND {dbAppointmentSearchCriteria[values[1].Substring(9, 1)]}='@val9';";
                            com.Parameters.AddWithValue("@val1", values[2]);
                            com.Parameters.AddWithValue("@val2", values[3]);
                            com.Parameters.AddWithValue("@val3", values[4]);
                            com.Parameters.AddWithValue("@val4", values[5]);
                            com.Parameters.AddWithValue("@val5", values[6]);
                            com.Parameters.AddWithValue("@val6", values[7]);
                            com.Parameters.AddWithValue("@val7", values[8]);
                            com.Parameters.AddWithValue("@val8", values[9]);
                            com.Parameters.AddWithValue("@val9", values[10]);
                            unsyncCom.CommandText = $"SELECT * FROM '{loc}' WHERE {dbAppointmentSearchCriteria[values[1].Substring(1, 1)]}='@val1' AND {dbAppointmentSearchCriteria[values[1].Substring(2, 1)]}='@val2' AND {dbAppointmentSearchCriteria[values[1].Substring(3, 1)]}='@val3' AND {dbAppointmentSearchCriteria[values[1].Substring(4, 1)]}='@val4' AND {dbAppointmentSearchCriteria[values[1].Substring(5, 1)]}='@val5' AND {dbAppointmentSearchCriteria[values[1].Substring(6, 1)]}='@val6' AND {dbAppointmentSearchCriteria[values[1].Substring(7, 1)]}='@val7' AND {dbAppointmentSearchCriteria[values[1].Substring(8, 1)]}='@val8' AND {dbAppointmentSearchCriteria[values[1].Substring(9, 1)]}='@val9';";
                            unsyncCom.Parameters.AddWithValue("@val1", values[2]);
                            unsyncCom.Parameters.AddWithValue("@val2", values[3]);
                            unsyncCom.Parameters.AddWithValue("@val3", values[4]);
                            unsyncCom.Parameters.AddWithValue("@val4", values[5]);
                            unsyncCom.Parameters.AddWithValue("@val5", values[6]);
                            unsyncCom.Parameters.AddWithValue("@val6", values[7]);
                            unsyncCom.Parameters.AddWithValue("@val7", values[8]);
                            unsyncCom.Parameters.AddWithValue("@val8", values[9]);
                            unsyncCom.Parameters.AddWithValue("@val9", values[10]);
                        }
                    }
                    
                    
                    using SQLiteDataReader syncReader = com.ExecuteReader();
                    List<string> result = new List<string>();
                    while (syncReader.Read())
                    {
                        result.Add($"{syncReader.GetInt32(0)}^{syncReader.GetString(1)}^{syncReader.GetString(2)}^{syncReader.GetString(3)}^{syncReader.GetString(4)}^{syncReader.GetString(5)}^{syncReader.GetString(6)}^{syncReader.GetString(7)}^{syncReader.GetString(8)}");
                    }
                    using SQLiteDataReader unsyncReader = unsyncCom.ExecuteReader();
                    unsyncCom.CommandType = System.Data.CommandType.Text;
                    while (unsyncReader.Read())
                    {
                        result.Add($"{unsyncReader.GetInt32(0)}^{unsyncReader.GetString(1)}'^{unsyncReader.GetString(2)}^{unsyncReader.GetString(3)}^{unsyncReader.GetString(4)}^{unsyncReader.GetString(5)}^{unsyncReader.GetString(6)}^{unsyncReader.GetString(7)}^{unsyncReader.GetString(8)}^{unsyncReader.GetString(9)}");
                    }
                    return SortUnsyncMultiResult(result);
                }
            }
            #endregion
            #region Sort Unsync Data
            public static List<string> SortUnsyncMultiResult(List<string> result)
            {
                // The Big O Notation is screaming at me for this one; this is an optimization I'll have to come back to if I have time due to it's messiness
                // The Optimization I would place would be to use Hashing, to create a third table of hashvalues of what the latest HashValue of what I'm trying to access is, e.g (PatientID: 00001 HashID:010101)
                // It would be faster to read compared to sorting.
                List<string> sorted = new List<string>();
                List<string[]> storage = new List<string[]>();
                    foreach (string item in result)
                    {
                        char[] split = new char[] { '^' };
                        string[] items = item.Split(split);
                        storage.Add(items);
                    }
                    for (int i = 0; i <= storage.Count;)
                    {
                        string[] str = storage[i];
                        foreach(string[] itemArray in storage)
                        {
                            List<string[]> dupe = new List<string[]>();
                            int count = 0;
                            try { 
                                if (itemArray[1] == str[1])
                                {
                                    if(itemArray[0] != str[0]) { 
                                        dupe.Add(str);
                                        count++;
                                    }
                                }
                                else
                                {
                                int length = str.Length - 1;
                                string store = "";
                                for (int ite = 1; ite <= length;)
                                {
                                    // Reasoning behind my for method:
                                    // I need to skip exID which is idStorage[ite]
                                    // I also need to skip mode with is the last item of idStorage hence the idStorage.Length -1 being set as the comparison value
                                    store = store + "^" + str[ite];
                                    ite++;
                                    // 
                                }
                                sorted.Add(store);
                            }
                            }
                            finally
                            {
                                if (count > 0)
                                {
                                   
                                    string[] idStorage = { };
                                    foreach (string[] exID in dupe)
                                    {
                                        if(Convert.ToInt32(exID[0]) > Convert.ToInt32(idStorage[0]))
                                        {
                                            idStorage = exID;
                                        }
                                    }

                                    string store = "";
                                    int len = idStorage.Length - 1;
                                    for(int ite = 1; ite <= len;)
                                    {
                                        // Reasoning behind my for method:
                                        // I need to skip exID which is idStorage[ite]
                                        // I also need to skip mode with is the last item of idStorage hence the idStorage.Length -1 being set as the comparison value
                                        store = store + "^" + idStorage[ite];
                                        ite++;

                                    }
                                    sorted.Add(store);
                                }
                            }
                            
                        }
                        
                        i++;
                    }
              return sorted;

            }
            #endregion
            #region Insert Data
            /// <summary>
            /// Inserts Data into a specified field (WIP function as it only has auth added, will add Patient and User table after
            /// </summary>
            /// <param name="dataType"></param>
            /// <param name="dataString"></param>
            /// <param name="dataIdentifier"></param>
            /// <returns></returns>
            public static bool InsertData(string dataType, string dataString, string dataIdentifier,bool unique)
            {
                using (var con = new SQLiteConnection("Data Source=pms.db"))
                {
                    string insertComm = "";
                    switch (dataType) { 
                        case "auth":
                         insertComm = $"UPDATE unsyncUser SET session = '{dataString}' WHERE username = '{dataIdentifier}';";
                        break;
                    }
                    var unsyncCom = con.CreateCommand();
                    con.Open();
                    unsyncCom.CommandText = insertComm;
                    unsyncCom.CommandType = System.Data.CommandType.Text;
                    unsyncCom.ExecuteNonQuery();
                    con.Close();
                    return true;
                }
            }
            public static bool InsertData(string dataType, string mode, string dataString)
            {
                /*
                 * This method is the alternative to InsertData(string, string, string); Does the same thing but without 
                 */
                using(var con = new SQLiteConnection("Data Source=pms.db"))
                {
                    string insertComm = "";
                    if(dataType == "P")
                    {
                        dataString = dataString.Replace("-", " ");
                        insertComm = $"INSERT INTO 'main'.'unsyncPatients'('Mode', 'PatientID', 'FName', 'LName', 'DateOfBirth', 'HouseNo.', 'AddressLine1', 'Postcode', 'PhoneNumber', 'EmailAddress') VALUES(@mode, @dstring); ";
                    }
                    else if(dataType == "U")
                    {

                    }
                    var unsyncCom = con.CreateCommand();
                    con.Open();

                    unsyncCom.CommandText = insertComm;
                    unsyncCom.Parameters.AddWithValue("@mode", mode);
                    unsyncCom.Parameters.AddWithValue("@dstring", dataString);
                    unsyncCom.CommandType = System.Data.CommandType.Text;
                    unsyncCom.ExecuteNonQuery();
                    con.Close();
                    return true;
                }
            }
            #endregion
            #region Update Data
            //Not used until Network Sync; unsyncPatients has turned into an instruction set (need to move onto network implemetation)
            public static bool UpdateData(string dataLocation,string dataIdentifier, string dataString)
            {
                using(var con = new SQLiteConnection("Data Source=pms.db"))
                {
                    string updateComm;
                    if(dataLocation == "P")
                    {
                        updateComm = $"UPDATE 'main'.'unsyncPatients' SET 'FName'=@fname, 'LName'=@lname, 'DateOfBirth'=@dob, 'HouseNo.'=@house, 'AddressLine1'=@adr1, 'Postcode'=@pc, 'PhoneNumber'=@tel, 'EmailAddress'=@emailadr WHERE 'PatientID'=@patientID;";
                        return true;
                    }
                    return false;
                }
            }
            #endregion
            #region Login Management
            // Defined as login management due to the login specific functions.
            public static bool CompareHashData(string tableID, string checkValue, string compareValue)
            {
                UTF8Encoding UTFConverter = new UTF8Encoding();
                string dbData = GetData(checkValue, tableID);
                if (UTFConverter.GetString(RSAManager.Decrypt(Convert.FromBase64String(dbData))) == compareValue)
                    return true;
                else
                    return false;
            }
            public static bool CheckUserSession(string mode, string hashValue, string username)
            {
                switch (mode) {
                    case "check":
                        if (hashValue == GetData(username, "1a"))
                            return true;
                        else
                            return false;

                }
                return false;

                
            }
            public static string UserSession(string mode, string hashValue, string uname) 
            {
                //This is run a lot, I'm using a switch case for the small optimization it provides at the amount of times this is function
                switch (mode)
                {
                    case "gen":
                        Random rndValue = new Random();
                        string sessionValue = "";
                        string potentialCharValue = "aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrRsStTuUvVwWxXyYzZ0123456789@[]";
                        int iterationCount = rndValue.Next(16, 32);
                        for (int x = 0; x <= iterationCount; x++)
                        {
                           sessionValue = sessionValue + potentialCharValue.Substring(rndValue.Next(1, 38), 1);
                        }
                        InsertData("auth", sessionValue, uname, true);
                        return sessionValue;
                    case "close":
                        break;
                }
                return "";
            }
            #endregion
        }
        class PermissionsManager
        {
            // Due to the odd one or two values that provide different results to the standard True or False this has to return the value to the STD_OUT for the Frontend to Process (SEE Worker.cs Part of the PatientManagementSystem Solution)
            public static string SendPermissionRequest(string username, string permissionCheck) => DatabaseManager.GetData("PERM_REQ", DatabaseManager.GetData(username, "1g"), permissionCheck); // Nice one liner, this sends to the Function Send Data first a request for the usergroup of our user then sends a Request for the Result of the Column of Our usergroup by getting the row and column by groupid.
        }

        #endregion
        #region Networking | Finish other sections first (expected functionality of Patient Management, Appointment Management, Staff Management.
        /*
         * The Network Implementation isn't going to be done until DB Functions are in place
         * This is so I have a full comprehensable DB Structure I can transfer between systems
         * Whereas I'd like to implement Delta Compression now, I do not have an understanding of what DB Structure will be in place in the futures
         * Any tasks I need to do is on https://pms.callumwalton.uk/?controller=BoardViewController&action=show&project_id=1
         */
        class TCPClient
        {
            static Int32 port = 11000;
            static String server = "127.0.0.1";
            static TcpClient client = new TcpClient(server, port);
            static NetworkStream clientNS = client.GetStream();
            public static void Client_HandlePacketRequest(string Packet)
            {
                switch ("Packet")
                {
                    case "SendUnsyncDB_Req":
                        break;

                }

            }
            // void on connection send UID Request Packet
            // On UID Request Packet Recieved if Unsynced DB contains anything then send a synchronization request 
        }
        class TCPServer
        {
            public static TcpListener server;
            public static bool listen = true;
            public static Dictionary<int,TcpClient> tcpClientDict;
            public static Dictionary<int, NetworkStream> tcpClientNSDict;
            public static List<TcpClient> clientList;
            
            public TCPServer()
            {
                IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ip = ipHost.AddressList[0];
                IPEndPoint localEndPoint = new IPEndPoint(ip, 11000);
                server = new TcpListener(localEndPoint);

                server.Start();
                StartTCPListen();
            }
            // Often Identified as a TCPListener
            
            public static void StartTCPListen()
            {
                
                try { 
                    while (listen)
                    {
                        Random rnd = new Random();
                        TcpClient client = server.AcceptTcpClient();
                        int uid = rnd.Next(100, 999);
                        tcpClientDict.Add(uid, client);
                        clientList.Add(client);
                        tcpClientNSDict.Add(uid, client.GetStream());
                        Task.Run(() => HandleClient(uid));
                    }
                }
                catch(SocketException ex)
                {
                    server.Stop();
                }
            }
            
            public static void HandleClient(int uid)
            {
                byte[] buffer = new byte[2048];
                int i;
                String data = null;
                while((i = tcpClientNSDict[uid].Read(buffer, 0,buffer.Length)) != 0)
                {
                    data = Encoding.ASCII.GetString(buffer, 0, i);
                    if (data == "UIDReq")
                    {
                        tcpClientNSDict[uid].Write(Encoding.ASCII.GetBytes(uid.ToString()));
                    }
                    
                }

                
            }
            public static void Server_HandlePacketRequest(string Packet)
            {
                switch (Packet)
                {
                    case "UIDReq":
                        break;
                    case "LatestSync":
                        break;
                    case "Master":
                        break;
                    case "Slave":
                        break;
                }
            }
           
        }
        class UDPBroadcastManager
        {
            
        } // To be implemented
        #endregion // Won't look at this region for a little
    }
}
