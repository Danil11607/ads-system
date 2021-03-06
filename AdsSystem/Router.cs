﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AdsSystem.Controllers;
using Microsoft.AspNetCore.Http;
using HeyRed.Mime; 

namespace AdsSystem
{
    
    public class Router
    {
        private static RouterDictionary _routes = new RouterDictionary()
        {
            {@"GET ^\/$", "IndexController.Index"},
            {@"GET ^\/login$", "IndexController.Login"},
            {@"POST ^\/login$", "IndexController.LoginHandler"},
            {@"GET ^\/logout$", "IndexController.Logout"},
            {@"GET ^\/api/zone/([0-9]+)", "ApiController.Get"},
            {@"GET ^\/api/click/([0-9]+)", "ApiController.Click"},
        } + 
             UsersController.GetRoutes() + 
             ZonesController.GetRoutes() +
             BannersController.GetRoutes();

        private static void _res(HttpResponse res, string body)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(body);
            res.Body.Write(buffer, 0, buffer.Length);
            res.Body.Close();
        }

        private static void _invokeAction(string selectedAction, HttpRequest request, HttpResponse response, object[] parameters = null)
        {
            var action = selectedAction.Split('.');
            var baseNamespace = typeof(Router).Namespace.Split('.')[0];
            var cls = Type.GetType(baseNamespace + ".Controllers." + action[0]);

            if (cls == null)
            {
                response.StatusCode = 500;
                _res(response, "cannot find action class");
                return;
            }

            var controller = Activator.CreateInstance(cls);

            var reqProp = cls.GetProperty("Request");
            var resProp = cls.GetProperty("Response");

            if (reqProp == null || resProp == null)
            {
                response.StatusCode = 500;
                _res(response, "controller is not contains Request and Response attributes");
                return;
            }

            reqProp.SetValue(controller, request);
            resProp.SetValue(controller, response);
            
            bool isNeedInvokeAction = true;
            string res = "";
            
            var beforeActionMethod = cls.GetMethod("BeforeAction");
            if (beforeActionMethod != null)
                isNeedInvokeAction = (bool) beforeActionMethod.Invoke(controller, new [] { request.Method, action[0], action[1] });

            if (isNeedInvokeAction)
            {
                res = (string) cls.GetMethod(action[1]).Invoke(controller, cls
                    .GetMethod(action[1])
                    .GetParameters()
                    .Select((x, key) => key < parameters?.Length && parameters[key] != null ? parameters[key] : Missing.Value)
                    .ToArray());
            }
            response = (HttpResponse) resProp.GetValue(controller);

            _res(response, res);
        }
        
        public static void Dispatch(HttpRequest request, HttpResponse response)
        {
            try
            {
                var url = request.Path.Value;
                string selectedAction = null;
                string[] parameters = null;

                foreach (var route in _routes)
                {
                    var key = route.Key.Split(' ');
                    var httpMethod = key[0];

                    if (request.Method != httpMethod)
                        continue;

                    var r = Regex.Matches(url, key[1]);
                    var check = r.Count > 0;
                    
                    if (check)
                    {
                        selectedAction = route.Value;
                        parameters = r.SelectMany(x => x.Groups).Skip(1).Select(x => x.Value).ToArray();
                        break;
                    }
                }
                
                if (selectedAction != null)
                    // ReSharper disable once CoVariantArrayConversion
                    _invokeAction(selectedAction, request, response, parameters);
                else
                {
                    var urlArr = url.Substring(1).Split('?');
                    var staticFilePath = Path.Combine(Environment.CurrentDirectory, "public", urlArr[0]);
                    
                    if (!File.Exists(staticFilePath))
                        _invokeAction("ErrorController.E404", request, response);
                    else
                    {
                        selectedAction = "static";
                        
                        string mime;
                        if (url.Contains("jpg"))
                            mime = "image/jpeg";
                        else if (url.Contains("js"))
                            mime = "text/javascript";
                        else 
                            mime = MimeTypesMap.GetMimeType(Path.GetFileName(url));
                        
                        response.Headers["Content-Type"] = mime;
                        
                        FileInfo fInfo = new FileInfo(staticFilePath);
                        long numBytes = fInfo.Length;
                        
                        using (var br = new BinaryReader(new FileStream(staticFilePath, FileMode.Open, FileAccess.Read)))
                        {
                            byte[] bOutput = br.ReadBytes((int)numBytes);   
                            response.Body.Write(bOutput, 0, bOutput.Length);   
                        }
                        response.Body.Close();
                    }
                }
                Console.WriteLine(request.Method + " " + url + " " + (selectedAction != null ? "ok" : "error") +
                                  " " + DateTime.Now + ":" + DateTime.Now.Millisecond);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                response.StatusCode = 500;
                _res(response, "Exception: " + e.Message);
            }
        } 
    }
}