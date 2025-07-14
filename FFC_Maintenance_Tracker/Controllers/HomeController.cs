using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.SqlClient;
using FFC_Maintenance_Tracker.Models;
using System.Globalization;
using System.IO;

namespace FFC_Maintenance_Tracker.Controllers
{
    public class HomeController : Controller
    {
        //Conexion de base de datos
        SqlConnection DBEmployee = new SqlConnection("Data Source=HMX-VENTUKDB ;Initial Catalog=VENTUK_HISENSE ;User ID=prac01; password=password123");
        SqlConnection AMIFCC = new SqlConnection("Data Source=172.29.72.15 ;Initial Catalog=AMI_Request ;User ID=sa; password=newshamu");
        SqlCommand con = new SqlCommand();
        SqlDataReader dr;

        List<Usuarios> ListadoEmpleados = new List<Usuarios>();
        List<Reportes> ListadoReportes = new List<Reportes>();
        List<Reportes> QeuryListadoReportes = new List<Reportes>();
        List<Registros> ListadoRegistros = new List<Registros>();
        List<Detalles> ListadoDetalles = new List<Detalles>();

        [HttpGet]
        public JsonResult GetEmployeeInfo(string employeeId)
        {
            string username = null, area = null, department = null, hmx = null;
            string imagenBase64ITRevirw = null;

            using (SqlCommand cmd = new SqlCommand("SELECT * FROM ListadoEmpleadosDepartamento WHERE ID_Empleado = @ID AND Estatus = 'A'", DBEmployee))
            {
                cmd.Parameters.AddWithValue("@ID", employeeId);
                DBEmployee.Open();

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        username = reader["NombreCompleto"].ToString();
                        hmx = "HMX" + reader["FolioHisense"].ToString();
                        area = reader["Area"].ToString();
                        department = reader["Departamento1"].ToString();

                        if (reader["Foto"] != DBNull.Value)
                        {
                            byte[] imageBytes = (byte[])reader["Foto"];
                            imagenBase64ITRevirw = "data:image/jpeg;base64," + Convert.ToBase64String(imageBytes);
                        }
                    }
                }

                DBEmployee.Close();

                if (!string.IsNullOrEmpty(username))
                {
                    return Json(new
                    {
                        success = true,
                        employee = new
                        {
                            fullName = username,
                            hmx = hmx,
                            area = area,
                            department = department,
                            photoUrl = string.IsNullOrEmpty(imagenBase64ITRevirw)
                                ? Url.Content("~/Images/default-user.png")
                                : imagenBase64ITRevirw
                        }
                    }, JsonRequestBehavior.AllowGet);
                }
            }

            return Json(new { success = false, message = "Empleado no encontrado" }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GenerarPDFAll(string folio)
        {
            

            using (var stream = new MemoryStream())
            {

                string folioDate = DateTime.Now.ToString("yyyyMMddHHmmss");
                return File(stream.ToArray(), "application/pdf", $"Reporte_{folio + " | " + folio + " | " + folioDate}.pdf");
            }
        }

        [HttpPost]
        public ActionResult GuardarRevisiones(string Select, string OperadorFCT, string folio, string ModeloInterno, string Hora, string Numero)
        {
            string usuario = Session["Username"].ToString();
            // Convertir "6:30 AM" a formato 24h "06:30"
            DateTime horaConvertida = DateTime.ParseExact(Hora.Trim(), "h:mm tt", CultureInfo.InvariantCulture);
            string hora24 = horaConvertida.ToString("HH:mm");

            string updateQuery = "";
            switch (Numero)
            {
                case "1":
                    updateQuery = @"UPDATE AMI_FCCSystemsRecords SET 
                            DateReview1 = @Fecha, 
                            UserRevidew1 = @Usuario, 
                            Setp1_One = @Valor 
                        WHERE Folio = @ID AND Timed = @Timed";
                    break;
                case "2":
                    updateQuery = @"UPDATE AMI_FCCSystemsRecords SET 
                            DateReview2 = @Fecha, 
                            UserRevidew2 = @Usuario, 
                            Setp2_Two = @Valor 
                        WHERE Folio = @ID AND Timed = @Timed";
                    break;
                case "3":
                    updateQuery = @"UPDATE AMI_FCCSystemsRecords SET 
                            DateReview3 = @Fecha, 
                            UserRevidew3 = @Usuario, 
                            Setp3_Trh = @Valor 
                        WHERE Folio = @ID AND Timed = @Timed";
                    break;
                default:
                    throw new Exception("Número de revisión inválido");
            }

            using (SqlCommand command = new SqlCommand(updateQuery, AMIFCC))
            {
                AMIFCC.Open();

                command.Parameters.AddWithValue("@Fecha", DateTime.Now);
                command.Parameters.AddWithValue("@Usuario", usuario);
                command.Parameters.AddWithValue("@Valor", Select);
                command.Parameters.AddWithValue("@ID", folio);
                command.Parameters.AddWithValue("@Timed", hora24);

                int rowsAffected = command.ExecuteNonQuery();

                AMIFCC.Close();
            }

            return RedirectToAction("Report", new { id = folio});

        }

        public ActionResult Menu()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        [HttpPost]
        public ActionResult GenReport(string Line, string WhoCreate, string Internal, string Placa, string Station)
        {
            ViewBag.username = Session["Username"];

            if (Session.Count <= 0)
            {
                return RedirectToAction("LogIn", "Login");
            }
            else
            {
                //data is correctly variables index!
                string Area = null, Departament = null;

                SqlCommand NAME = new SqlCommand("Select * from ListadoEmpleadosDepartamento " +
                            " where NombreCompleto = '" + WhoCreate + "' and Estatus = 'A'", DBEmployee);
                DBEmployee.Open();
                SqlDataReader drNAME = NAME.ExecuteReader();
                if (drNAME.HasRows)
                {
                    while (drNAME.Read())
                    {
                        Area = drNAME["Area"].ToString();
                        Departament = drNAME["Departamento1"].ToString();
                    }
                }
                DBEmployee.Close();

                string createFol = DateTime.Now.ToString("yyyyMMddHHmmss");
                string username = Session["Username"].ToString();

                // Guardar información en base de datos
                AMIFCC.Open();
                SqlCommand cmd = new SqlCommand("INSERT INTO AMI_FCCSystemsFolio " +
                    "(Estacion,Folio, Line, Who_create, DateAdded, DatetimtimeAdded, Active,InternalModel,PCBA,Status,QRMain,Area,Departament,Operator) VALUES " +
                    "(@Estacion,@Folio, @Line, @Who_create, GETDATE(), GETDATE(),@Active,@InternalModel,@PCBA,@Status,@QRMain,@Area,@Departament,@Operator)", AMIFCC);

                cmd.Parameters.AddWithValue("@Estacion", Station);
                cmd.Parameters.AddWithValue("@Area", Area);
                cmd.Parameters.AddWithValue("@Departament", Departament);
                cmd.Parameters.AddWithValue("@Operator", WhoCreate);

                cmd.Parameters.AddWithValue("@QRMain", Placa.ToUpper());
                cmd.Parameters.AddWithValue("@Folio", "AMI" + createFol);
                cmd.Parameters.AddWithValue("@Line", "L"+ Line);
                cmd.Parameters.AddWithValue("@Who_create", WhoCreate.ToString());
                cmd.Parameters.AddWithValue("@Active", true);
                cmd.Parameters.AddWithValue("@InternalModel", Internal.ToUpper());
                cmd.Parameters.AddWithValue("@PCBA", "-");
                cmd.Parameters.AddWithValue("@Status", "INCOMPLETE");
                cmd.ExecuteNonQuery();
                AMIFCC.Close();

                List<string> Tiempos = new List<string>
{
    "06:30", "07:30", "08:30", "09:30", "10:30", "11:30","12:30",
    "13:30", "14:30", "15:30", "16:30", "17:30", "18:30","19:30","20:30","21:30","22:30","23:30","01:30"
    ,"02:30","03:30","04:30","05:30"
};

                AMIFCC.Open();
                foreach (var tiempo in Tiempos)
                {
                    using (SqlCommand cmd2 = new SqlCommand(@"INSERT INTO AMI_FCCSystemsRecords 
                        (Folio, SN_PlacaMain, UserReview, Timed, Setp1_One, Setp2_Two, Setp3_Trh, UserRevidew1, UserRevidew2, UserRevidew3) 
                        VALUES 
                        (@Folio, @SN_PlacaMain, @UserReview, @Timed, @Setp1_One, @Setp2_Two, @Setp3_Trh ,@UserRevidew1 ,@UserRevidew2, @UserRevidew3)", AMIFCC))
                    {
                        cmd2.Parameters.AddWithValue("@Folio", "AMI" + createFol);
                        cmd2.Parameters.AddWithValue("@SN_PlacaMain", "Pending");
                        cmd2.Parameters.AddWithValue("@UserReview", "Pending");
                        cmd2.Parameters.AddWithValue("@Timed", tiempo); // Aquí corregido
                        cmd2.Parameters.AddWithValue("@Setp1_One", "-");
                        cmd2.Parameters.AddWithValue("@Setp2_Two", "-");
                        cmd2.Parameters.AddWithValue("@Setp3_Trh", "-");

                        cmd2.Parameters.AddWithValue("@UserRevidew1", "-");
                        cmd2.Parameters.AddWithValue("@UserRevidew2", "-");
                        cmd2.Parameters.AddWithValue("@UserRevidew3", "-");
                        cmd2.ExecuteNonQuery();
                    }
                }
                AMIFCC.Close();

                ReportesListed(username);
                ViewBag.Record = ListadoReportes;
                ViewBag.count = ListadoReportes.Count;
                ViewBag.message = "";
                return View();
            }
        }

        public ActionResult GenReport()
        {
            ViewBag.username = Session["Username"];

            if (Session.Count <= 0)
            {
                return RedirectToAction("LogIn", "Login");
            }
            else
            {
                string username = Session["Username"].ToString();
                ReportesListed(username);
                ViewBag.Record = ListadoReportes;
                ViewBag.count = ListadoReportes.Count;
                ViewBag.message = "";

                string lines = null;
                // Obtener tipo de usuario
                SqlCommand queryType = new SqlCommand("SELECT * FROM AMI_FCCSystemsUsers WHERE Active = '1' AND Username = @username", AMIFCC);
                queryType.Parameters.AddWithValue("@username", username);

                AMIFCC.Open();
                SqlDataReader drqueryType = queryType.ExecuteReader();
                if (drqueryType.Read())
                {
                    lines = drqueryType["Line"].ToString();
                }
                AMIFCC.Close();

                string results = null;

                if (lines == "All")
                {
                    results = "All";
                }
                else
                {
                    results = lines.Length > 1 ? lines.Substring(1) : string.Empty;
                }

                ViewBag.Line = results;

                int registrosActualizados = 0;

                string selectQuery = "SELECT ID, Folio, DatetimtimeAdded, Status FROM AMI_FCCSystemsFolio WHERE Active = '1'";

                using (SqlCommand selectCommand = new SqlCommand(selectQuery, AMIFCC))
                {
                    AMIFCC.Open();
                    SqlDataReader reader = selectCommand.ExecuteReader();

                    var registros = new List<(int ID, string Folio, DateTime DateAdded, string Status)>();

                    while (reader.Read())
                    {
                        int id = Convert.ToInt32(reader["ID"]);
                        string folio = reader["Folio"].ToString();
                        string status = reader["Status"].ToString();
                        DateTime dateAdded;

                        if (DateTime.TryParse(reader["DatetimtimeAdded"].ToString(), out dateAdded))
                        {
                            registros.Add((id, folio, dateAdded /* sin .Date */, status));
                        }
                    }
                    AMIFCC.Close();

                    AMIFCC.Open();
                    foreach (var r in registros)
                    {
                        if (r.Status != "COMPLETE" && DateTime.Now >= r.DateAdded.AddHours(24))
                        {
                            string updateQuery = "UPDATE AMI_FCCSystemsFolio SET Status = @Status WHERE ID = @ID";
                            using (SqlCommand updateCommand = new SqlCommand(updateQuery, AMIFCC))
                            {
                                updateCommand.Parameters.AddWithValue("@Status", "COMPLETE");
                                updateCommand.Parameters.AddWithValue("@ID", r.ID);

                                int result = updateCommand.ExecuteNonQuery();

                                if (result > 0)
                                    registrosActualizados++;
                            }
                        }
                    }
                    AMIFCC.Close();
                }

                Console.WriteLine($"Se actualizaron {registrosActualizados} registros.");
                return View();
            }
        }

        public ActionResult Users()
        {
            ViewBag.username = Session["Username"];

            if (Session.Count <= 0)
            {
                return RedirectToAction("LogIn", "Login");
            }
            else
            {
                UsuariosListed();
                ViewBag.Record = ListadoEmpleados;
                ViewBag.count = ListadoEmpleados.Count;
                ViewBag.message = "";
                return View();
            }
        }

        [HttpPost]
        public ActionResult Users(string TypeUsers, string Credentials, string Pass, string Line, string Station, string Shift)
        {
            ViewBag.username = Session["Username"];

            if (Session.Count <= 0)
            {
                return RedirectToAction("LogIn", "Login");
            }
            else
            {
                //data is correctly variables index!
                string Username = null,Area = null, Departament = null, hmx = null;

                SqlCommand NAME = new SqlCommand("Select * from ListadoEmpleadosDepartamento " +
                            " where ID_Empleado = '" + Credentials + "' and Estatus = 'A'", DBEmployee);
                DBEmployee.Open();
                SqlDataReader drNAME = NAME.ExecuteReader();
                if (drNAME.HasRows)
                {
                    while (drNAME.Read())
                    {
                        Username = drNAME["NombreCompleto"].ToString();
                        Area = drNAME["Area"].ToString();
                        Departament = drNAME["Departamento1"].ToString();
                        hmx = "HMX" + drNAME["FolioHisense"].ToString();
                    }
                }
                else
                {
                    Username = "0";
                }
                DBEmployee.Close();

                //validacion de usuario | si no existe

                if (Username == "0")
                {
                    UsuariosListed();
                    ViewBag.Record = ListadoEmpleados;
                    ViewBag.count = ListadoEmpleados.Count;

                    ViewBag.message = "Message: User no found ! please try again . . . ";
                    return RedirectToAction("Users", "Home");
                }
                else
                {
                    string val = null;
                    SqlCommand Diplicatequery = new SqlCommand("Select * from AMI_FCCSystemsUsers where Active = '1' and EmployeeNumber = '" + Credentials + "'", AMIFCC);
                    AMIFCC.Open();
                    SqlDataReader drDiplicatequery = Diplicatequery.ExecuteReader();
                    if (drDiplicatequery.HasRows)
                    {
                        while (drDiplicatequery.Read())
                        {
                            val = drDiplicatequery["Username"].ToString();
                        }
                    }
                    else
                    {
                        val = "no duplicate";
                    }
                    AMIFCC.Close();

                    if (val == "no duplicate")
                    {
                        string NewLine = null;

                        if (TypeUsers == "Administrator")
                        {
                            Station = "All";
                            NewLine = "All";
                        }
                        else
                        {
                            NewLine = "L" + Line;
                        }

                        //Guardar informacion a la base de datos del proyecto
                        AMIFCC.Open();
                        SqlCommand PalletControl = new SqlCommand("insert into AMI_FCCSystemsUsers" +
                            "(Shift,EstacionAssig,Line, Type, Username, password, EmployeeNumber, HMX, Departament, Area, DateAdded, DatetimeAdded, Active) values " +
                            "(@Shift,@EstacionAssig,@Line,@Type, @Username, @password, @EmployeeNumber, @HMX, @Departament, @Area, getdate(), getdate(), @Active) ", AMIFCC);
                        //--------------------------------------------------------------------------------------------------------------------------------
                        PalletControl.Parameters.AddWithValue("@EstacionAssig", Station.ToString());
                        PalletControl.Parameters.AddWithValue("@Shift", Shift.ToString());
                        PalletControl.Parameters.AddWithValue("@Line", NewLine.ToString());
                        PalletControl.Parameters.AddWithValue("@Type", TypeUsers.ToString());
                        PalletControl.Parameters.AddWithValue("@Username", Username.ToString());
                        PalletControl.Parameters.AddWithValue("@password", Pass.ToString());
                        PalletControl.Parameters.AddWithValue("@EmployeeNumber", Credentials.ToString());
                        PalletControl.Parameters.AddWithValue("@HMX", hmx.ToString());
                        //PalletControl.Parameters.AddWithValue("@Dates", AddTxt_test1.Text);
                        //PalletControl.Parameters.AddWithValue("@Datetimes", AddTxt_test2.Text);
                        PalletControl.Parameters.AddWithValue("@Area", Area.ToString());
                        PalletControl.Parameters.AddWithValue("@Departament", Departament.ToString());
                        PalletControl.Parameters.AddWithValue("@Active", true);
                        PalletControl.ExecuteNonQuery();
                        AMIFCC.Close();

                        ViewBag.message = "";

                        UsuariosListed();
                        ViewBag.Record = ListadoEmpleados;
                        ViewBag.count = ListadoEmpleados.Count;
                        return RedirectToAction("Users", "Home");
                    }
                    else
                    {
                        UsuariosListed();
                        ViewBag.Record = ListadoEmpleados;
                        ViewBag.count = ListadoEmpleados.Count;

                        ViewBag.message = "Message: This username already exists. Please choose a different username";
                        return RedirectToAction("Users", "Home");
                    }
                }
            }
        }

        [HttpPost]
        public JsonResult DeleteProcess(string ID)
        {
            // Validación de sesión por cookie
            if (Session["Username"] == null && Request.Cookies["UserCookie"] != null)
            {
                Session["Username"] = Request.Cookies["UserCookie"].Value;
            }

            // Si no hay sesión válida, redirige a login
            if (Session.Count <= 0)
            {
                return Json(new { success = false, redirectUrl = Url.Action("LogIn", "Login") });
            }

            int rowsAffected = 0;
            int userCount = 0;

            // Actualizar usuario a inactivo
            string updateQuery = "UPDATE AMI_FCCSystemsUsers SET Active = @Active WHERE HMX = @ID";
            using (SqlCommand command = new SqlCommand(updateQuery, AMIFCC))
            {
                AMIFCC.Open();
                command.Parameters.AddWithValue("@Active", false);
                command.Parameters.AddWithValue("@ID", ID);
                rowsAffected = command.ExecuteNonQuery();
                AMIFCC.Close();
            }

            // Si se actualizó correctamente, contar usuarios activos
            if (rowsAffected > 0)
            {
                string countQuery = "SELECT COUNT(*) FROM AMI_FCCSystemsUsers WHERE Active = 1";
                using (SqlCommand countCommand = new SqlCommand(countQuery, AMIFCC))
                {
                    AMIFCC.Open();
                    userCount = (int)countCommand.ExecuteScalar();
                    AMIFCC.Close();
                }
            }

            // Retornar JSON con resultado y nuevo contador
            return Json(new
            {
                success = rowsAffected > 0,
                userCount = userCount
            });
        }

        [HttpPost]
        public JsonResult RemoveFolio(string ID)
        {
            // Validación de sesión por cookie
            if (Session["Username"] == null && Request.Cookies["UserCookie"] != null)
            {
                Session["Username"] = Request.Cookies["UserCookie"].Value;
            }

            // Si no hay sesión válida, redirige a login
            if (Session.Count <= 0)
            {
                return Json(new { success = false, redirectUrl = Url.Action("LogIn", "Login") });
            }

            int rowsAffected = 0;
            int userCount = 0;

            // Actualizar usuario a inactivo
            string updateQuery = "UPDATE AMI_FCCSystemsFolio SET Active = @Active WHERE Folio = @ID";
            using (SqlCommand command = new SqlCommand(updateQuery, AMIFCC))
            {
                AMIFCC.Open();
                command.Parameters.AddWithValue("@Active", false);
                command.Parameters.AddWithValue("@ID", ID);
                rowsAffected = command.ExecuteNonQuery();
                AMIFCC.Close();
            }

            // Si se actualizó correctamente, contar usuarios activos
            if (rowsAffected > 0)
            {
                string countQuery = "SELECT COUNT(*) FROM AMI_FCCSystemsFolio WHERE Active = 1";
                using (SqlCommand countCommand = new SqlCommand(countQuery, AMIFCC))
                {
                    AMIFCC.Open();
                    userCount = (int)countCommand.ExecuteScalar();
                    AMIFCC.Close();
                }
            }

            // Retornar JSON con resultado y nuevo contador
            return Json(new
            {
                success = rowsAffected > 0,
                userCount = userCount
            });
        }

        public ActionResult Report(string id)
        {
            ViewBag.username = Session["Username"];

            if (Session.Count <= 0)
            {
                return RedirectToAction("LogIn", "Login");
            }
            else
            {
                string internalMd = null, PCBA = null, Whocreate = null, Station = null;
                // Obtener tipo de usuario
                SqlCommand queryType = new SqlCommand("SELECT * FROM AMI_FCCSystemsFolio " +
                    " WHERE Active = '1' AND Folio = @Folio", AMIFCC);
                queryType.Parameters.AddWithValue("@Folio", id);

                AMIFCC.Open();
                SqlDataReader drqueryType = queryType.ExecuteReader();
                if (drqueryType.Read())
                {
                    internalMd = drqueryType["InternalModel"].ToString();
                    PCBA = drqueryType["PCBA"].ToString();
                    Whocreate = drqueryType["Who_create"].ToString();
                    Station = drqueryType["QRMain"].ToString();
                }
                AMIFCC.Close();

                string connectionString = "Data Source=172.29.72.15 ;Initial Catalog=AMI_Request ;User ID=sa; password=newshamu";
                using (SqlConnection AMIFCC = new SqlConnection(connectionString))
                {
                    string query = @"SELECT [ID], [Folio], [SN_PlacaMain], [UserReview], [Timed],
                            [Setp1_One], [DateReview1], [UserRevidew1],
                            [Setp2_Two], [DateReview2], [UserRevidew2],
                            [Setp3_Trh], [DateReview3], [UserRevidew3]
                     FROM [AMI_Request].[dbo].[AMI_FCCSystemsRecords]
                     WHERE Folio = @Folio";

                    using (SqlCommand command = new SqlCommand(query, AMIFCC))
                    {
                        command.Parameters.AddWithValue("@Folio", id); // Asegúrate que 'id' está definido

                        AMIFCC.Open();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                ListadoRegistros.Add(new Registros()
                                {
                                    ID = Convert.ToInt32(reader["ID"]),
                                    Folio = reader["Folio"].ToString(),
                                    SN_PlacaMain = reader["SN_PlacaMain"].ToString(),
                                    UserReview = reader["UserReview"].ToString(),
                                    Timed = reader["Timed"].ToString(),
                                    Setp1_One = reader["Setp1_One"].ToString(),
                                    DateReview1 = reader["DateReview1"].ToString(),
                                    UserRevidew1 = reader["UserRevidew1"].ToString(),
                                    Setp2_Two = reader["Setp2_Two"].ToString(),
                                    DateReview2 = reader["DateReview2"].ToString(),
                                    UserRevidew2 = reader["UserRevidew2"].ToString(),
                                    Setp3_Trh = reader["Setp3_Trh"].ToString(),
                                    DateReview3 = reader["DateReview3"].ToString(),
                                    UserRevidew3 = reader["UserRevidew3"].ToString()
                                });
                            }
                        }
                        AMIFCC.Close();
                    }
                }
                ViewBag.RecordFiles = ListadoRegistros;

                // Obtener tipo de usuario
                string dates = null, status = null;
                DateTime? fechaBase = null;

                SqlCommand DateSelected = new SqlCommand("SELECT * FROM AMI_FCCSystemsFolio " +
                    "WHERE Active = '1' AND Folio = @Folio", AMIFCC);
                DateSelected.Parameters.AddWithValue("@Folio", id);

                AMIFCC.Open();
                SqlDataReader drDateSelected = DateSelected.ExecuteReader();
                if (drDateSelected.Read())
                {
                    status = drDateSelected["Status"].ToString();
                    dates = drDateSelected["DateAdded"].ToString();
                    DateTime parsedDate;
                    if (DateTime.TryParse(dates, out parsedDate))
                    {
                        fechaBase = parsedDate.Date;
                    }
                }
                AMIFCC.Close();

                int rowsAffected = 0;

                // Solo ejecutar UPDATE si ya pasó al siguiente día
                if (fechaBase.HasValue && DateTime.Now.Date > fechaBase.Value)
                {
                    string updateQuery = "UPDATE AMI_FCCSystemsFolio SET Status = @Status WHERE Folio = @ID";
                    using (SqlCommand command = new SqlCommand(updateQuery, AMIFCC))
                    {
                        AMIFCC.Open();
                        command.Parameters.AddWithValue("@Status", "COMPLETE");
                        command.Parameters.AddWithValue("@ID", id);
                        rowsAffected = command.ExecuteNonQuery();
                        AMIFCC.Close();
                    }
                }


                ViewBag.ModeloInterno = internalMd;
                ViewBag.stations = Station;
                ViewBag.OperadorFCT = Whocreate;
                ViewBag.id = id;
                ViewBag.message = "";
                ViewBag.dates = dates;
                ViewBag.status = status;

                var resultados = new Dictionary<string, Dictionary<int, string>>();

                using (SqlConnection con = new SqlConnection("Data Source=172.29.72.15; Initial Catalog=AMI_Request; User ID=sa; password=newshamu"))
                {
                    con.Open();

                    string query = @"SELECT Timed, Setp1_One, Setp2_Two, Setp3_Trh 
                     FROM AMI_Request.dbo.AMI_FCCSystemsRecords 
                     WHERE Folio = @Folio";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@Folio", id);

                        var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            string hora = "";
                            if (reader["Timed"] != DBNull.Value)
                            {
                                DateTime horaDb;
                                if (DateTime.TryParse(reader["Timed"].ToString(), out horaDb))
                                {
                                    hora = horaDb.ToString("h:mm tt", CultureInfo.InvariantCulture); // "1:30 PM"
                                }
                            }

                            string paso1 = reader["Setp1_One"] != DBNull.Value ? reader["Setp1_One"].ToString() : "";
                            string paso2 = reader["Setp2_Two"] != DBNull.Value ? reader["Setp2_Two"].ToString() : "";
                            string paso3 = reader["Setp3_Trh"] != DBNull.Value ? reader["Setp3_Trh"].ToString() : "";

                            if (!string.IsNullOrEmpty(hora)) // evita guardar si no se pudo convertir
                            {
                                resultados[hora] = new Dictionary<int, string>
                {
                    { 1, paso1 },
                    { 2, paso2 },
                    { 3, paso3 }
                };
                            }
                        }
                    }
                }

                ViewBag.ResultadosRevision = resultados;

                

                return View();
            }
        }

        [HttpGet]
        public ActionResult Historys(string DateInitial, string DateFinal)
        {
            ViewBag.username = Session["Username"];

            if (Session.Count <= 0)
            {
                return RedirectToAction("LogIn", "Login");
            }
            else
            {
                return RedirectToAction("History", "Home", new { value  = "1", DateInitial  = DateInitial , DateFinal  = DateFinal });
            }
        }

        public ActionResult History(string value , string DateInitial, string DateFinal)
        {
            if (value == null | value == "")
            {
                value = "";
            }

            string user = Session["Username"].ToString();

            // Obtener tipo de usuario
            SqlCommand Refresh = new SqlCommand("SELECT * FROM AMI_FCCSystemsUsers WHERE Active = '1' AND Username = @username", AMIFCC);
            Refresh.Parameters.AddWithValue("@username", user);

            AMIFCC.Open();
            SqlDataReader drRefresh = Refresh.ExecuteReader();
            if (drRefresh.Read())
            {
                ViewBag.Type = drRefresh["Type"].ToString();
            }
            AMIFCC.Close();

            ViewBag.username = Session["Username"];
         
            if (Session.Count <= 0)
            {
                return RedirectToAction("LogIn", "Login");
            }
            else
            {
                if (value == "1")
                {
                    if (DateInitial == "")
                    {
                        DateInitial = null;
                    }

                    if (DateFinal == "")
                    {
                        DateFinal = null;
                    }

                    int count = 0;

                    if (DateInitial != null)
                    {
                        count++;
                    }

                    if (DateFinal != null)
                    {
                        count++;
                    }

                    if (count == 2)
                    {
                        string username = Session["Username"].ToString();

                        string type = null;
                        // Obtener tipo de usuario
                        SqlCommand queryType = new SqlCommand("SELECT Type FROM AMI_FCCSystemsUsers WHERE Active = '1' AND Username = @username", AMIFCC);
                        queryType.Parameters.AddWithValue("@username", username);

                        AMIFCC.Open();
                        SqlDataReader drqueryType = queryType.ExecuteReader();
                        if (drqueryType.Read())
                        {
                            type = drqueryType["Type"].ToString();
                        }
                        AMIFCC.Close();

                        if (QeuryListadoReportes.Count > 0)
                        {
                            QeuryListadoReportes.Clear();
                        }

                        // Preparar consulta según tipo
                        string sqlQuery = "";

                        if (type == "Administrator")
                        {
                            sqlQuery = "SELECT top (100) * FROM AMI_FCCSystemsFolio WHERE Active = '1' and DateAdded between '" + DateInitial + "' and '" + DateFinal + "'";
                        }
                        else
                        {
                            sqlQuery = "SELECT top (100) * FROM AMI_FCCSystemsFolio WHERE Active = '1' AND Who_create = @username and DateAdded between '" + DateInitial + "' and '" + DateFinal + "'";
                        }

                        AMIFCC.Open();
                        SqlCommand con = new SqlCommand(sqlQuery, AMIFCC);
                        if (type != "administrador")
                        {
                            con.Parameters.AddWithValue("@username", username);
                        }

                        SqlDataReader dr = con.ExecuteReader();
                        while (dr.Read())
                        {
                            QeuryListadoReportes.Add(new Reportes()
                            {
                                folio = dr["Folio"].ToString(),
                                line = dr["Line"].ToString(),
                                WhoCreate = dr["Who_create"].ToString(),
                                Date = dr["DatetimtimeAdded"].ToString(),
                                InternalModel = dr["InternalModel"].ToString(),
                                PlacaMain = dr["QRMain"].ToString(),
                                Estacion = dr["Estacion"].ToString(),
                                Status = dr["Status"].ToString(),
                            });
                        }
                        AMIFCC.Close();

                        ViewBag.Record = QeuryListadoReportes;
                        ViewBag.count = QeuryListadoReportes.Count;
                        ViewBag.message = "";
                        return View();
                    }
                    else
                    {
                        string username = Session["Username"].ToString();
                        ReportesListed(username);
                        ViewBag.Record = ListadoReportes;
                        ViewBag.count = ListadoReportes.Count;
                        ViewBag.message = "Indica el rango de fecha correctamente";
                        return View();
                    }
                }
                else
                {
                    // Obtener tipo de usuario
                    string line = null;
                    SqlCommand queryType = new SqlCommand("SELECT * FROM AMI_FCCSystemsUsers WHERE Active = '1' AND Username = @username", AMIFCC);
                    queryType.Parameters.AddWithValue("@username", user);

                    AMIFCC.Open();
                    SqlDataReader drqueryType = queryType.ExecuteReader();
                    if (drqueryType.Read())
                    {
                        ViewBag.Type = drqueryType["Type"].ToString();
                        line = drqueryType["Line"].ToString();
                    }
                    AMIFCC.Close();

                    //string username = Session["Username"].ToString();
                    ReportesListed(user);
                    ViewBag.Record = ListadoReportes;
                    ViewBag.count = ListadoReportes.Count;
                    ViewBag.message = "";

                    int registrosActualizados = 0;

                    string selectQuery = "SELECT ID, Folio, DateAdded, Status FROM AMI_FCCSystemsFolio WHERE Active = '1'";

                    using (SqlCommand selectCommand = new SqlCommand(selectQuery, AMIFCC))
                    {
                        AMIFCC.Open();
                        SqlDataReader reader = selectCommand.ExecuteReader();

                        List<(int ID, string Folio, DateTime DateAdded, string Status)> registros = new List<(int, string, DateTime, string)>();

                        while (reader.Read())
                        {
                            int id = Convert.ToInt32(reader["ID"]);
                            string folio = reader["Folio"].ToString();
                            string status = reader["Status"].ToString();
                            DateTime dateAdded;

                            if (DateTime.TryParse(reader["DateAdded"].ToString(), out dateAdded))
                            {
                                registros.Add((id, folio, dateAdded.Date, status));
                            }
                        }
                        AMIFCC.Close();

                        foreach (var r in registros)
                        {
                            if (r.Status != "COMPLETE" && DateTime.Now.Date > r.DateAdded)
                            {
                                string updateQuery = "UPDATE AMI_FCCSystemsFolio SET Status = @Status WHERE ID = @ID";
                                using (SqlCommand updateCommand = new SqlCommand(updateQuery, AMIFCC))
                                {
                                    updateCommand.Parameters.AddWithValue("@Status", "COMPLETE");
                                    updateCommand.Parameters.AddWithValue("@ID", r.ID);

                                    AMIFCC.Open();
                                    int result = updateCommand.ExecuteNonQuery();
                                    AMIFCC.Close();

                                    if (result > 0)
                                        registrosActualizados++;
                                }
                            }
                        }
                    }
                }

                return View();
            }
        }

        public ActionResult Review(string id)
        {
            ViewBag.username = Session["Username"];

            if (Session.Count <= 0)
            {
                return RedirectToAction("LogIn", "Login");
            }
            else
            {
                RecordsAllData(id);
                ViewBag.Record = ListadoDetalles;
                ViewBag.id = id;

                // Obtener tipo de usuario
                SqlCommand queryType = new SqlCommand("SELECT * FROM AMI_FCCSystemsFolio WHERE Active = '1' AND Folio = @username", AMIFCC);
                queryType.Parameters.AddWithValue("@username", id);

                AMIFCC.Open();
                SqlDataReader drqueryType = queryType.ExecuteReader();
                if (drqueryType.Read())
                {
                    ViewBag.internalmodel = drqueryType["InternalModel"].ToString();
                    ViewBag.pcba = drqueryType["PCBA"].ToString();
                    ViewBag.operadorFCT = drqueryType["Operator"].ToString();
                }
                AMIFCC.Close();

                return View();
            }
        }

        private void UsuariosListed()
        {
            if (ListadoEmpleados.Count > 0)
            {
                ListadoEmpleados.Clear();
            }
            else
            {
                AMIFCC.Open();
                con.Connection = AMIFCC;
                con.CommandText = "select * from AMI_FCCSystemsUsers where Active = '1'";
                dr = con.ExecuteReader();
                while (dr.Read())
                {
                    ListadoEmpleados.Add(new Usuarios()
                    {
                        Type = dr["Type"].ToString(),
                        Username = (dr["Username"].ToString()),
                        password = (dr["password"].ToString()),
                        HMX = (dr["HMX"].ToString()),
                        Departament = (dr["Departament"].ToString()),
                        Area = (dr["Area"].ToString()),
                        DatetimeAdded = (dr["DatetimeAdded"].ToString()),
                        Line = (dr["Line"].ToString()),
                        Estacion = (dr["EstacionAssig"].ToString()),
                    });
                }
                AMIFCC.Close();
            }
        }

        private void ReportesListed(string user)
        {
            string type = null, line = null;
            // Obtener tipo de usuario
            SqlCommand queryType = new SqlCommand("SELECT Type,Line FROM AMI_FCCSystemsUsers WHERE Active = '1' AND Username = @username", AMIFCC);
            queryType.Parameters.AddWithValue("@username", user);

            AMIFCC.Open();
            SqlDataReader drqueryType = queryType.ExecuteReader();
            if (drqueryType.Read())
            {
                type = drqueryType["Type"].ToString();
                line = drqueryType["Line"].ToString();
            }
            AMIFCC.Close();

            if (ListadoReportes.Count > 0)
            {
                ListadoReportes.Clear();
            }

            // Preparar consulta según tipo
            string sqlQuery = "";

            if (type == "Administrator")
            {
                sqlQuery = "SELECT top (100) * FROM AMI_FCCSystemsFolio WHERE Active = '1' order by Folio desc";
            }
            else
            {
                sqlQuery = "SELECT top (100) * FROM AMI_FCCSystemsFolio WHERE Active = '1' AND Line = @Line order by Folio desc";
            }

            AMIFCC.Open();
            SqlCommand con = new SqlCommand(sqlQuery, AMIFCC);
            if (type != "administrador")
            {
                con.Parameters.AddWithValue("@Line", line);
            }

            SqlDataReader dr = con.ExecuteReader();
            while (dr.Read())
            {
                ListadoReportes.Add(new Reportes()
                {
                    folio = dr["Folio"].ToString(),
                    line = dr["Line"].ToString(),
                    WhoCreate = dr["Who_create"].ToString(),
                    Date = dr["DatetimtimeAdded"].ToString(),
                    InternalModel = dr["InternalModel"].ToString(),
                    PlacaMain = dr["QRMain"].ToString(),
                    Estacion = dr["Estacion"].ToString(),
                    Status = dr["Status"].ToString(),
                });
            }
            AMIFCC.Close();

        }

        private void RecordsAllData(string id)
        {
            if (ListadoDetalles.Count > 0)
            {
                ListadoDetalles.Clear();
            }
            else
            {
                AMIFCC.Open();
                con.Connection = AMIFCC;
                con.CommandText = "select * from AMI_FCCSystemsRecords where Folio = '" + id + "' order by Timed asc";
                dr = con.ExecuteReader();
                while (dr.Read())
                {
                    ListadoDetalles.Add(new Detalles()
                    {
                        time = dr["Timed"].ToString(),
                        Setp1_One = (dr["Setp1_One"].ToString()),
                        DateReview1 = (dr["DateReview1"].ToString()),
                        UserReview1 = (dr["UserRevidew1"].ToString()),

                        Setp2_Two = (dr["Setp2_Two"].ToString()),
                        DateReview2 = (dr["DateReview2"].ToString()),
                        UserReview2 = (dr["UserRevidew2"].ToString()),

                        Setp3_Trh = (dr["Setp3_Trh"].ToString()),
                        DateReview3 = (dr["DateReview3"].ToString()),
                        UserReview3 = (dr["UserRevidew3"].ToString()),
                    });
                }
                AMIFCC.Close();
            }
        }
    }
}

