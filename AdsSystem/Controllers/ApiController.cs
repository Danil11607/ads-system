﻿using System;
using System.Collections.Generic;
using System.Linq;
using AdsSystem.Libs;
using Microsoft.EntityFrameworkCore;

namespace AdsSystem.Controllers
{
    public class ApiController : ControllerBase
    {
        public string Get(string zoneId)
        {
            int id;
            int.TryParse(zoneId, out id);
            Response.StatusCode = 400;

            using (var db = Db.Instance)
            {
                var where = db.BannersZones.Where(x => x.ZoneId == id);
                var count = where.Count();
                
                if (count == 0)
                {
                    Response.StatusCode = 404;
                    return "";
                }
                
                var r = new Random();
                var index = r.Next(0, where.Count());
                var res = where.Skip(index).First();
                
                if (res == null)
                    return "";
                
                var banner = db.Banners.Find(res.BannerId);
                var zone = db.Zones.Find(res.Zone);

                var view = new Models.View();
                db.Entry(banner).State = EntityState.Unchanged;
                db.Entry(zone).State = EntityState.Unchanged;
                view.Banner = banner;
                view.Zone = zone;
                view.UserAgent = Request.Headers["User-Agent"];
                db.Add(view);
                db.SaveChanges();
                
                var linkBase = Request.Protocol + Request.Host; 
                var clickLink = linkBase + "/api/click/" + view.Id;
                
                Response.StatusCode = 200;
                return View("ApiReturn", new Dictionary<string, object>
                {
                    {"Height", zone.Height},
                    {"Height", zone.Width},
                    {"ClickLink", clickLink},
                    {"Type", banner.Type},
                    {"Html", banner.Html},
                    {"ImageLink", banner.ImageFormat != null ? (linkBase + FileStorage.GetLink(banner, banner.ImageFormat)) : ""}    
                });
            }
        }

        public string Click(string viewId)
        {
            int id;
            int.TryParse(viewId, out id);
            using (var db = Db.Instance)
            {
                var view = db.Views.Find(id);
                view.IsClicked = true;
                db.Attach(view);
                db.SaveChanges();
                
                Response.StatusCode = 202;
                return "";
            }
        }
    }
}