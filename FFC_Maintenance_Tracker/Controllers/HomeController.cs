using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.SqlClient;
using FFC_Maintenance_Tracker.Models;
using System.Globalization;
using System.IO;
using System.Configuration;
using System.Net.Mail;
using System.Net;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using System.Text.RegularExpressions;

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

        public ActionResult PingSession()
        {
            // Solo para mantener viva la sesión
            var user = Session["Username"];
            return Content("OK");
        }

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

        public ActionResult TestEmail()
        {
            SendEmail("panchodelgadozam@gmail.com", "Correo de prueba", "Este es un test de envío de correo desde ASP.NET MVC.");
            return Content("Correo enviado.");
        }

        public void EnviarSMS(string numeroDestino, string mensaje)
    {
        var message = MessageResource.Create(
            body: mensaje,
            from: new PhoneNumber("+15186768198"), // tu número comprado en Twilio
            to: new PhoneNumber(numeroDestino)
        );

        Console.WriteLine($"Mensaje enviado a {numeroDestino}: {message.Sid}");
    }

        [HttpPost]
        public ActionResult CheckIncompleteReport(string bloqueId, string linea)
        {
            // Validar existencia de sesión desde cookies
            if (Session["Username"] == null && Request.Cookies["UserCookie"] != null)
            {
                Session["Username"] = Request.Cookies["UserCookie"].Value;
            }

            if (Session["Type"] == null && Request.Cookies["TypeCookie"] != null)
            {
                Session["Type"] = Request.Cookies["TypeCookie"].Value;
            }

            // Si no hay sesión, redirigir al login
            if (Session["Username"] == null || Session["Type"] == null)
            {
                return RedirectToAction("LogIn", "Login");
            }

            string lineaBD = null, estacionBD = null, estadoReporte = null;

            try
            {
                using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["AMIFCC"].ConnectionString))
                using (SqlCommand cmd = new SqlCommand("SELECT Line, Estacion, Estado FROM AMI_FCCSystemsFolio WHERE Folio = @folio AND Active = '1'", conn))
                {
                    cmd.Parameters.AddWithValue("@folio", linea);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            lineaBD = reader["Line"]?.ToString();
                            estacionBD = reader["Estacion"]?.ToString();
                            estadoReporte = reader["Estado"]?.ToString(); // Aquí obtienes el estado
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Manejo básico de error
                ViewBag.Folio = "Error al consultar los datos del reporte: " + ex.Message;
                return PartialView("_NotificacionReporte");
            }

            ViewBag.Folio = linea;
            ViewBag.Linea = lineaBD;
            ViewBag.Estacion = estacionBD;
            ViewBag.Hora = DateTime.Now.ToString("HH:mm");
            ViewBag.Status = (estadoReporte != null && estadoReporte.ToUpper() == "INCOMPLETE") ? "INCOMPLETE" : "CERRADO";

            return PartialView("_NotificacionReporte");
        }

        [HttpPost]
        public JsonResult CheckAndSendEmail(int bloqueId)
        {
            string folio = "";
            bool correoEnviado = false;

            try
            {
                using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["Data Source=172.29.72.15 ;Initial Catalog=AMI_Request ;User ID=sa; password=newshamu"].ConnectionString))
                using (SqlCommand cmd = new SqlCommand(@"
            SELECT TOP 1 Folio 
            FROM [AMI_Request].[dbo].[AMI_FCCSystemsFolio] 
            WHERE Status = 'INCOMPLETE' AND Active = 1 AND ID = @BloqueId", conn))
                {
                    cmd.Parameters.AddWithValue("@BloqueId", bloqueId);
                    conn.Open();
                    var result = cmd.ExecuteScalar();
                    if (result != null)
                    {
                        folio = result.ToString();

                        // Aquí envías el correo
                        string subject = "Reporte INCOMPLETO - Minuto 20 alcanzado";
                        string body = $"El reporte con folio {folio} ha alcanzado los 20 minutos y sigue INCOMPLETO.";

                        SendEmail("panchodelgadozam@gmail.com", subject, body);
                        correoEnviado = true;
                    }
                }
            }
            catch (Exception ex)
            {
                // Loguear si es necesario
            }

            return Json(new { enviado = correoEnviado, folio = folio });
        }

        private void SendEmail(string to, string subject, string body)
        {
            string gmailUser = "ffcmain7@gmail.com";
            string gmailPassword = "fcot tscn wuws qsxh";

            //System.Diagnostics.Debug.WriteLine($"GmailUser: '{gmailUser}'");
            //System.Diagnostics.Debug.WriteLine($"GmailAppPassword: '{gmailPassword}'");

            var mail = new MailMessage();
            mail.To.Add(to);
            mail.Subject = subject;
            mail.Body = body;
            mail.IsBodyHtml = false;
            mail.From = new MailAddress(gmailUser, "FFC Cable Inspection and Control Notification");

            using (var smtp = new SmtpClient("smtp.gmail.com"))
            {
                smtp.Port = 587;
                smtp.Credentials = new NetworkCredential(gmailUser, gmailPassword);
                smtp.EnableSsl = true;
                smtp.Send(mail);
            }
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
        public ActionResult GuardarRevisiones(string Select, string OperadorFCT, string folio, string Hora, string Numero, string Usuario)
        {
            Hora = Regex.Replace(Hora, @"\s+", " ").Trim();

            // Validar existencia de sesión, recuperando desde cookies si es necesario
            if (Session["Username"] == null && Request.Cookies["UserCookie"] != null)
            {
                Session["Username"] = Request.Cookies["UserCookie"].Value;
            }

            if (Session["Type"] == null && Request.Cookies["TypeCookie"] != null)
            {
                Session["Type"] = Request.Cookies["TypeCookie"].Value;
            }

            // Si no hay sesión, redirigir al login
            if (Session["Username"] == null || Session["Type"] == null)
            {
                return RedirectToAction("LogIn", "Login");
            }

            ViewBag.username = Session["Username"];

            //string usuario = Session["Username"].ToString();
            // Solo convertir si contiene ':'

            //data is correctly variables index!

            //realizar un string y seleccionar unicamente los 6 primeros caracteres
            string Number = Usuario.ToString();
            Number = Number.Substring(0, 6);
            Number = Number.Substring((Number.Length - 5), 5);
            Usuario = Number;
            int convertion = Int32.Parse(Number);

            //-----------------------------------------------------------------
            //indicar si el valor es mayor de 1000 indica a numero de empleado mas viejos
            //-----------------------------------------------------------------

            if (convertion <= 10)
            {
                Number = Number.Substring((Number.Length - 1), 1);
                Usuario = Number;
            }
            else
            {
                //-----------------------------------------------------------------
                if (convertion <= 100)
                {
                    Number = Number.Substring((Number.Length - 2), 2);
                    Usuario = Number;
                }
                else
                {
                    //-----------------------------------------------------------------
                    if (convertion <= 1000)
                    {
                        Number = Number.Substring((Number.Length - 3), 3);
                        Usuario = Number;
                    }
                    else
                    {
                        //-----------------------------------------------------------------
                        if (convertion > 1000 && convertion <= 10000)
                        {
                            Number = Number.Substring((Number.Length - 4), 4);
                            Usuario = Number;
                        }
                        else
                        {
                            //-----------------------------------------------------------------
                            if (convertion > 10000)
                            {
                                Number = Number.Substring((Number.Length - 5), 5);
                                Usuario = Number;
                            }
                        }
                    }
                }
            }
        
            string usernames = null;

            SqlCommand NAME = new SqlCommand("Select * from ListadoEmpleadosDepartamento " +
                        " where ID_Empleado = '" + Usuario + "' and Estatus = 'A'", DBEmployee);
            DBEmployee.Open();
            SqlDataReader drNAME = NAME.ExecuteReader();
            if (drNAME.HasRows)
            {
                while (drNAME.Read())
                {
                    usernames = drNAME["NombreCompleto"].ToString();
                }
            }
            else
            {
                usernames = "/";
            }
            DBEmployee.Close();

            if (usernames == "/")
            {
                return RedirectToAction("Report", new { id = folio });
            }
            else
            {
                try
                {
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
                        command.Parameters.AddWithValue("@Usuario", usernames);
                        command.Parameters.AddWithValue("@Valor", Select);
                        command.Parameters.AddWithValue("@ID", folio);
                        command.Parameters.AddWithValue("@Timed", hora24);

                        int rowsAffected = command.ExecuteNonQuery();

                        AMIFCC.Close();
                    }

                    return RedirectToAction("Report", new { id = folio });
                }
                catch (FormatException ex)
                {
                    // Retorna un error detallado al cliente (JS)
                    return Json(new
                    {
                        success = false,
                        message = $"Formato de Hora inválido: '{Hora}'"
                    });
                }

            }
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
        public ActionResult GenReport(string Line, string WhoCreate, string Internal, string Placa, string Station, string Credentials)
        {
            // Validar existencia de sesión, recuperando desde cookies si es necesario
            if (Session["Username"] == null && Request.Cookies["UserCookie"] != null)
            {
                Session["Username"] = Request.Cookies["UserCookie"].Value;
            }

            if (Session["Type"] == null && Request.Cookies["TypeCookie"] != null)
            {
                Session["Type"] = Request.Cookies["TypeCookie"].Value;
            }

            // Si no hay sesión, redirigir al login
            if (Session["Username"] == null || Session["Type"] == null)
            {
                return RedirectToAction("LogIn", "Login");
            }

            ViewBag.username = Session["Username"];

            // Si no hay sesión, redirigir al login
            if (Session["Username"] == null || Session["Type"] == null)
            {
                return RedirectToAction("LogIn", "Login");
            }

            ViewBag.username = Session["Username"];

            //data is correctly variables index!
            string Area = null, Departament = null, usernames = null;

                SqlCommand NAME = new SqlCommand("Select * from ListadoEmpleadosDepartamento " +
                            " where ID_Empleado = '" + Credentials + "' and Estatus = 'A'", DBEmployee);
                DBEmployee.Open();
                SqlDataReader drNAME = NAME.ExecuteReader();
                if (drNAME.HasRows)
                {
                    while (drNAME.Read())
                    {
                    usernames = drNAME["NombreCompleto"].ToString();
                    Area = drNAME["Area"].ToString();
                    Departament = drNAME["Departamento1"].ToString();
                    }
                }
                DBEmployee.Close();

                string createFol = DateTime.Now.ToString("yyyyMMddHHmmss");
                //string username = Session["Username"].ToString();

                // Guardar información en base de datos
                AMIFCC.Open();
                SqlCommand cmd = new SqlCommand("INSERT INTO AMI_FCCSystemsFolio " +
                    "(Estacion,Folio, Line, Who_create, DateAdded, DatetimtimeAdded, Active,InternalModel,PCBA,Status,QRMain,Area,Departament,Operator) VALUES " +
                    "(@Estacion,@Folio, @Line, @Who_create, GETDATE(), GETDATE(),@Active,@InternalModel,@PCBA,@Status,@QRMain,@Area,@Departament,@Operator)", AMIFCC);

                cmd.Parameters.AddWithValue("@Estacion", Station);
                cmd.Parameters.AddWithValue("@Area", Area);
                cmd.Parameters.AddWithValue("@Departament", Departament);
                cmd.Parameters.AddWithValue("@Operator", usernames);

                cmd.Parameters.AddWithValue("@QRMain", "-");
                cmd.Parameters.AddWithValue("@Folio", "AMI" + createFol);
                cmd.Parameters.AddWithValue("@Line", "L"+ Line);
                cmd.Parameters.AddWithValue("@Who_create", usernames.ToString());
                cmd.Parameters.AddWithValue("@Active", true);
                cmd.Parameters.AddWithValue("@InternalModel", "-");
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

                ReportesListed(usernames);
                ViewBag.Record = ListadoReportes;
                ViewBag.count = ListadoReportes.Count;
                ViewBag.message = "";

                string lines = null;
                // Obtener tipo de usuario
                SqlCommand queryType = new SqlCommand("SELECT * FROM AMI_FCCSystemsUsers WHERE Active = '1' AND Username = @username", AMIFCC);
                queryType.Parameters.AddWithValue("@username", usernames);

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

                return View();
            
        }

        public ActionResult GenReport()
        {
            // Validar existencia de sesión, recuperando desde cookies si es necesario
            if (Session["Username"] == null && Request.Cookies["UserCookie"] != null)
            {
                Session["Username"] = Request.Cookies["UserCookie"].Value;
            }

            if (Session["Type"] == null && Request.Cookies["TypeCookie"] != null)
            {
                Session["Type"] = Request.Cookies["TypeCookie"].Value;
            }

            // Si no hay sesión, redirigir al login
            if (Session["Username"] == null || Session["Type"] == null)
            {
                return RedirectToAction("LogIn", "Login");
            }

            ViewBag.username = Session["Username"];

            // Si no hay sesión, redirigir al login
            if (Session["Username"] == null || Session["Type"] == null)
            {
                return RedirectToAction("LogIn", "Login");
            }

            ViewBag.username = Session["Username"];

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

        public ActionResult Users()
        {
            // Validar existencia de sesión, recuperando desde cookies si es necesario
            if (Session["Username"] == null && Request.Cookies["UserCookie"] != null)
            {
                Session["Username"] = Request.Cookies["UserCookie"].Value;
            }

            if (Session["Type"] == null && Request.Cookies["TypeCookie"] != null)
            {
                Session["Type"] = Request.Cookies["TypeCookie"].Value;
            }

            // Si no hay sesión, redirigir al login
            if (Session["Username"] == null || Session["Type"] == null)
            {
                return RedirectToAction("LogIn", "Login");
            }

            ViewBag.username = Session["Username"];

            // Si no hay sesión, redirigir al login
            if (Session["Username"] == null || Session["Type"] == null)
            {
                return RedirectToAction("LogIn", "Login");
            }

            if (Session.Count <= 0)
            {
                return RedirectToAction("LogIn", "Login");
            }
            else
            {
                ViewBag.username = Session["Username"];
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
            // Validar existencia de sesión, recuperando desde cookies si es necesario
            if (Session["Username"] == null && Request.Cookies["UserCookie"] != null)
            {
                Session["Username"] = Request.Cookies["UserCookie"].Value;
            }

            if (Session["Type"] == null && Request.Cookies["TypeCookie"] != null)
            {
                Session["Type"] = Request.Cookies["TypeCookie"].Value;
            }

            // Si no hay sesión, redirigir al login
            if (Session["Username"] == null || Session["Type"] == null)
            {
                return RedirectToAction("LogIn", "Login");
            }

            ViewBag.username = Session["Username"];

            if (Session.Count <= 0)
            {
                return RedirectToAction("LogIn", "Login");
            }
            else
            {
                if (TypeUsers == "Produccion")
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
                    PalletControl.Parameters.AddWithValue("@EstacionAssig", "undefined");
                    PalletControl.Parameters.AddWithValue("@Shift", Shift.ToString());
                    PalletControl.Parameters.AddWithValue("@Line", NewLine.ToString());
                    PalletControl.Parameters.AddWithValue("@Type", TypeUsers.ToString());
                    PalletControl.Parameters.AddWithValue("@Username", NewLine.ToUpper());
                    PalletControl.Parameters.AddWithValue("@password", Pass.ToString());
                    PalletControl.Parameters.AddWithValue("@EmployeeNumber", "0");
                    PalletControl.Parameters.AddWithValue("@HMX", NewLine.ToUpper());
                    //PalletControl.Parameters.AddWithValue("@Dates", AddTxt_test1.Text);
                    //PalletControl.Parameters.AddWithValue("@Datetimes", AddTxt_test2.Text);
                    PalletControl.Parameters.AddWithValue("@Area", "undefined");
                    PalletControl.Parameters.AddWithValue("@Departament", "undefined");
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
                    //data is correctly variables index!
                    string Username = null, Area = null, Departament = null, hmx = null;

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
            // Validar existencia de sesión, recuperando desde cookies si es necesario
            if (Session["Username"] == null && Request.Cookies["UserCookie"] != null)
            {
                Session["Username"] = Request.Cookies["UserCookie"].Value;
            }

            if (Session["Type"] == null && Request.Cookies["TypeCookie"] != null)
            {
                Session["Type"] = Request.Cookies["TypeCookie"].Value;
            }

            // Si no hay sesión, redirigir al login
            if (Session["Username"] == null || Session["Type"] == null)
            {
                return RedirectToAction("LogIn", "Login");
            }

            ViewBag.username = Session["Username"];

            if (Session.Count <= 0)
            {
                return RedirectToAction("LogIn", "Login");
            }
            else
            {
                string internalMd = null, PCBA = null, Whocreate = null, Station = null, lines = null;
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
                    DateTime fechaOriginal = Convert.ToDateTime(drqueryType["DatetimtimeAdded"]);
                    ViewBag.Finish = fechaOriginal.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss");
                    ViewBag.lines = drqueryType["Line"].ToString();
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

                int registrosActualizados = 0;

                string selectQuery = "SELECT ID, Folio, DatetimtimeAdded, Status FROM AMI_FCCSystemsFolio WHERE Active = '1'";

                using (SqlCommand selectCommand = new SqlCommand(selectQuery, AMIFCC))
                {
                    AMIFCC.Open();
                    SqlDataReader reader = selectCommand.ExecuteReader();

                    var registros = new List<(int ID, string Folio, DateTime DateAdded, string Status)>();

                    while (reader.Read())
                    {
                        int ids = Convert.ToInt32(reader["ID"]);
                        string folio = reader["Folio"].ToString();
                        string statuss = reader["Status"].ToString();
                        DateTime dateAdded;

                        if (DateTime.TryParse(reader["DatetimtimeAdded"].ToString(), out dateAdded))
                        {
                            registros.Add((ids, folio, dateAdded /* sin .Date */, statuss));
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

                ViewBag.ModeloInterno = internalMd;
                ViewBag.stations = Station;
                ViewBag.OperadorFCT = Whocreate;
                ViewBag.id = id;
                ViewBag.message = "";
                ViewBag.dates = dates;

                string val = null;
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

                SqlCommand Diplicatequery = new SqlCommand("Select * from AMI_FCCSystemsFolio where Active = '1' and Folio = '" + id + "'", AMIFCC);
                AMIFCC.Open();
                SqlDataReader drDiplicatequery = Diplicatequery.ExecuteReader();
                if (drDiplicatequery.HasRows)
                {
                    while (drDiplicatequery.Read())
                    {
                        ViewBag.status = drDiplicatequery["Status"].ToString();
                    }
                }
                AMIFCC.Close();

                return View();
            }
        }

        [HttpGet]
        public ActionResult Historys(string DateInitial, string DateFinal)
        {
            // Validar existencia de sesión, recuperando desde cookies si es necesario
            if (Session["Username"] == null && Request.Cookies["UserCookie"] != null)
            {
                Session["Username"] = Request.Cookies["UserCookie"].Value;
            }

            if (Session["Type"] == null && Request.Cookies["TypeCookie"] != null)
            {
                Session["Type"] = Request.Cookies["TypeCookie"].Value;
            }

            // Si no hay sesión, redirigir al login
            if (Session["Username"] == null || Session["Type"] == null)
            {
                return RedirectToAction("LogIn", "Login");
            }

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

            // Validar existencia de sesión, recuperando desde cookies si es necesario
            if (Session["Username"] == null && Request.Cookies["UserCookie"] != null)
            {
                Session["Username"] = Request.Cookies["UserCookie"].Value;
            }

            if (Session["Type"] == null && Request.Cookies["TypeCookie"] != null)
            {
                Session["Type"] = Request.Cookies["TypeCookie"].Value;
            }

            // Si no hay sesión, redirigir al login
            if (Session["Username"] == null || Session["Type"] == null)
            {
                return RedirectToAction("LogIn", "Login");
            }

            ViewBag.username = Session["Username"];

            string user = Session["Username"].ToString();
         
            if (Session.Count <= 0)
            {
                return RedirectToAction("LogIn", "Login");
            }
            else
            {
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
                            ViewBag.Type = drqueryType["Type"].ToString();
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

                        string type = null;
                        // Obtener tipo de usuario
                        SqlCommand queryType = new SqlCommand("SELECT Type FROM AMI_FCCSystemsUsers WHERE Active = '1' AND Username = @username", AMIFCC);
                        queryType.Parameters.AddWithValue("@username", username);

                        AMIFCC.Open();
                        SqlDataReader drqueryType = queryType.ExecuteReader();
                        if (drqueryType.Read())
                        {
                            type = drqueryType["Type"].ToString();
                            ViewBag.Type = drqueryType["Type"].ToString();
                        }
                        AMIFCC.Close();

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
                }

                return View();
            }
        }

        public ActionResult Review(string id)
        {
            // Validar existencia de sesión, recuperando desde cookies si es necesario
            if (Session["Username"] == null && Request.Cookies["UserCookie"] != null)
            {
                Session["Username"] = Request.Cookies["UserCookie"].Value;
            }

            if (Session["Type"] == null && Request.Cookies["TypeCookie"] != null)
            {
                Session["Type"] = Request.Cookies["TypeCookie"].Value;
            }

            // Si no hay sesión, redirigir al login
            if (Session["Username"] == null || Session["Type"] == null)
            {
                return RedirectToAction("LogIn", "Login");
            }

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
                    ViewBag.line = drqueryType["Line"].ToString();
                    ViewBag.date = drqueryType["DatetimtimeAdded"].ToString();
                }
                AMIFCC.Close();

                using (SqlConnection AMIFCC = new SqlConnection("Data Source=172.29.72.15 ;Initial Catalog=AMI_Request ;User ID=sa; password=newshamu"))
                {
                    AMIFCC.Open(); // asegúrate de abrirla si no está abierta

                    string folio = id.ToString();

                    string sql = @"
    DECLARE @Folio VARCHAR(50) = @FolioParam;
    SELECT
        STUFF((
            SELECT DISTINCT
                '  |  ' + REPLACE(U.Username, '-', '') + ' (' + ISNULL(U.Shift, 'Sin Turno') + ')'
            FROM (
                SELECT UserRevidew1 AS Username
                FROM [AMI_Request].[dbo].[AMI_FCCSystemsRecords]
                WHERE Folio = @Folio AND UserRevidew1 IS NOT NULL

                UNION ALL

                SELECT UserRevidew2
                FROM [AMI_Request].[dbo].[AMI_FCCSystemsRecords]
                WHERE Folio = @Folio AND UserRevidew2 IS NOT NULL

                UNION ALL

                SELECT UserRevidew3
                FROM [AMI_Request].[dbo].[AMI_FCCSystemsRecords]
                WHERE Folio = @Folio AND UserRevidew3 IS NOT NULL
            ) AS Reviews
            LEFT JOIN [AMI_Request].[dbo].[AMI_FCCSystemsUsers] U
                ON U.Username = Reviews.Username
            FOR XML PATH(''), TYPE
        ).value('.', 'NVARCHAR(MAX)'), 1, 5, '') AS UsuariosConTurno;";

                    using (SqlCommand cmd = new SqlCommand(sql, AMIFCC))
                    {
                        cmd.Parameters.AddWithValue("@FolioParam", folio);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                ViewBag.UsuariosAsociados = reader["UsuariosConTurno"].ToString();
                            }
                        }
                    }

                    AMIFCC.Close(); // explícitamente cerrar si la abriste tú
                }


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

