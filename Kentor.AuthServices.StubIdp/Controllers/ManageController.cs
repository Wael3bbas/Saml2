﻿using System.IO;
using System.Net.Mime;
using Kentor.AuthServices.StubIdp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Web;
using System.Web.Mvc;
using Kentor.AuthServices.Mvc;
using System.IdentityModel.Metadata;
using Kentor.AuthServices.Configuration;
using System.IdentityModel.Tokens;
using System.Configuration;
using Kentor.AuthServices.Saml2P;
using Kentor.AuthServices.WebSso;
using Kentor.AuthServices.HttpModule;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Linq;

namespace Kentor.AuthServices.StubIdp.Controllers
{
    public class ManageController : BaseController
    {
        /// <summary>
        /// Special guid for the default IDP user list
        /// </summary>
        private static readonly Guid defaultIdpGuid = Guid.Parse("e73d98ff-0f1c-4cc2-8808-6d1bf028a8a9");

        public ActionResult Index(Guid idpId)
        {
            var fileName = GetIdpFileNamePath(idpId);
            var model = new ManageIdpModel();
            if (System.IO.File.Exists(fileName))
            {
                model.JsonData = System.IO.File.ReadAllText(fileName);
            }
            return View(model);
        }

        [HttpPost]
        public ActionResult Index(Guid idpId, ManageIdpModel model)
        {
            if (idpId == defaultIdpGuid)
            {
                ModelState.AddModelError("", "Can't update default model");
            }
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var schema = JSchema.Parse(System.IO.File.ReadAllText(Server.MapPath("~/Content/IdpConfigurationSchema.json")));

            JObject parsedJson;
            try
            {
                parsedJson = JObject.Parse(model.JsonData);
            }
            catch (Exception)
            {
                ModelState.AddModelError("JsonData", "Invalid Json");
                return View(model);
            }
            IList<string> errorMessages;
            if (!parsedJson.IsValid(schema, out errorMessages))
            {
                ModelState.AddModelError("JsonData", "Json does not match schema. " + string.Join(" ", errorMessages));
            }

            var fileName = GetIdpFileNamePath(idpId);

            model.JsonData = parsedJson.ToString(Newtonsoft.Json.Formatting.Indented);

            System.IO.File.WriteAllText(fileName, model.JsonData);

            cachedConfigurations.AddOrUpdate(idpId, new IdpConfigurationModel(model.JsonData), (_, __) => new IdpConfigurationModel(model.JsonData));

            return RedirectToAction("Index");
        }

        public ActionResult CurrentConfiguration(Guid? idpId)
        {
            var fileData = GetCachedConfiguration(idpId.GetValueOrDefault(defaultIdpGuid));
            if (fileData == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Internal server error, no IDP configured");
            }
            return Content(fileData.JsonData, "application/json");
        }
    }
}