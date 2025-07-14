//LIBRERIAS EN USO
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.SqlClient;

namespace FFC_Maintenance_Tracker.Controllers
{
    public class LoginController : Controller
    {
        //Conexion de base de datos

        SqlConnection AMIFCC = new SqlConnection("Data Source=172.29.72.15 ;Initial Catalog=AMI_Request ;User ID=sa; password=newshamu");
        SqlCommand con = new SqlCommand();
        SqlDataReader dr;
        // GET: Login
        public ActionResult LogIn()
        {
            return View();
        }

        [HttpPost]
        public ActionResult LogIn(string Password, string HMX)
        {
            //Query solicitud de datos en base de datos

            string Username = null, type = null;
            SqlCommand UserQuery = new SqlCommand("Select * from AMI_FCCSystemsUsers where Active = '1' and HMX = @HMX and password = @pass", AMIFCC);
            UserQuery.Parameters.AddWithValue("@HMX",HMX.ToUpper());
            UserQuery.Parameters.AddWithValue("@pass", Password.ToString());

            AMIFCC.Open();
            SqlDataReader drUserQuery = UserQuery.ExecuteReader();
            if (drUserQuery.HasRows)
            {
                // si lo encuentra - Solicitud de Nombre | Tipo de usuario
                while (drUserQuery.Read())
                {
                    Username = drUserQuery["Username"].ToString();
                    type = drUserQuery["Type"].ToString();
                }
            }
            else
            {
                // no lo encuentra
                Username = "0";
            }
            AMIFCC.Close();

            //validacion de usuario - Existe o no existe

            if (Username == "0")
            {
                ViewBag.message = "Message: Sorry, we couldn’t locate the user, please check and try again ";
                return View();
            }
            else
            {
                ViewBag.message = "";
                Session["Username"] = Username.ToString();
                Session["Type"] = type.ToString();

                return RedirectToAction("Menu", "Home");
            }
        }
    }
}