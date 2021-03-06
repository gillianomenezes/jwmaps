﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using JWMaps.Models;
using JWMaps.ViewModel;
using GoogleMaps.LocationServices;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity;
using System.Data.Entity;
using System.Configuration;

namespace JWMaps.Controllers
{
    [Authorize(Roles = RoleName.CanManageHouseholders + ", " + RoleName.CanAdministrate)]
    public class HouseholdersController : Controller
    {
        private ApplicationDbContext _context;

        public HouseholdersController()
        {
            _context = new ApplicationDbContext();
        }

        protected override void Dispose(bool disposing)
        {
            _context.Dispose();
        }
                
        // GET: Householder
        public ActionResult Index()
        {
            List<HouseholderViewModel> householderViewModels = new List<HouseholderViewModel>();
            var store = new UserStore<ApplicationUser>(new ApplicationDbContext());
            var userManager = new UserManager<ApplicationUser>(store);
            ApplicationUser user = userManager.FindByNameAsync(User.Identity.Name).Result;

            var householders = _context.Householders.Include(h => h.Visits).Where(h => h.CongregationId == user.CongregationId);
            
            foreach (var householder in householders)
            {
                var householderViewModel = new HouseholderViewModel
                {
                    Householder = householder
                };

                householderViewModels.Add(householderViewModel);
            }

            return View("ListHouseholders", householderViewModels);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Save(Householder householder)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var householderViewModel = new HouseholderViewModel
                    {
                        Householder = householder
                    };

                    return View("HouseholdersForm", householderViewModel);
                }

                var store = new UserStore<ApplicationUser>(new ApplicationDbContext());
                var userManager = new UserManager<ApplicationUser>(store);
                ApplicationUser user = userManager.FindByNameAsync(User.Identity.Name).Result;

                var locationService = new GoogleLocationService(ConfigurationManager.AppSettings["GooglePlaceAPIKey"]);
                var point = locationService.GetLatLongFromAddress(householder.Address + ", " + householder.Neighbourhood + "-" + householder.City);

                householder.Latitude = point.Latitude;
                householder.Longitude = point.Longitude;

                if (householder.Id == 0)
                {
                    householder.CreationDate = DateTime.Now;
                    householder.CongregationId = user.CongregationId;
                    _context.Householders.Add(householder);
                }
                else
                {
                    var householderdb = _context.Householders.Single(h => h.Id == householder.Id);

                    householderdb.Name = householder.Name;
                    householderdb.Neighbourhood = householder.Neighbourhood;
                    householderdb.Phone = householder.Phone;
                    householderdb.ZipCode = householder.ZipCode;
                    householderdb.CongregationId = user.CongregationId;
                    householderdb.Address = householder.Address;
                    householderdb.City = householder.City;
                    householderdb.Latitude = householder.Latitude;
                    householderdb.Longitude = householder.Longitude;
                    householderdb.Publisher = householder.Publisher;
                    householderdb.Observations = householder.Observations;
                    householderdb.Category = householder.Category;
                }

                _context.SaveChanges();

                return RedirectToAction("Index", "Householders");
            }
            catch (Exception e)
            {
                System.Threading.Thread.Sleep(1000);
                return ViewHouseholderData(householder.Id);
            }
        }

        private ApplicationUser GetUser()
        {
            var store = new UserStore<ApplicationUser>(new ApplicationDbContext());
            var userManager = new UserManager<ApplicationUser>(store);
            ApplicationUser user = userManager.FindByNameAsync(User.Identity.Name).Result;

            return user;
        }

        public ActionResult New()
        {
            var user = GetUser();

            var householderViewModel = new HouseholderViewModel
            {
                Householder = new Householder(),
                Publishers = _context.Publishers.Where(p => p.CongregationId == user.CongregationId)
            };

            return View("HouseholdersForm", householderViewModel);
        }


        public ActionResult ViewHouseholderData(int id)
        {
            if (User.IsInRole(RoleName.CanManageHouseholders) || User.IsInRole(RoleName.CanAdministrate))
            {
                return RedirectToAction("Edit", new { id = id });
            }
            else
            {
                return RedirectToAction("Details", new { id = id });
            }
        }


        [Authorize(Roles = RoleName.CanViewHouseholderData)]
        public ActionResult Details(int id)
        {
            var householderInDb = _context.Householders.Include(h => h.Visits).Single(h => h.Id == id);
            var publisherInDb = _context.Publishers.Single(p => p.Id == householderInDb.Publisher.Id);

            var detailHouseholderViewModel = new DetailsHouseholderViewModel
            {
                Householder = householderInDb,
                Publisher = publisherInDb
            };

            return View("Details", detailHouseholderViewModel);
        }

        [Authorize(Roles = RoleName.CanManageHouseholders + ", " + RoleName.CanAdministrate)]
        public ActionResult Edit(int id)
        {
            var user = GetUser();
            var householderViewModel = new HouseholderViewModel
            {
                Householder = _context.Householders.Include(h => h.Visits).Single(h => h.Id == id),
                Publishers = _context.Publishers.Where(p => p.CongregationId == user.CongregationId).ToList()
            };

            return View("HouseholdersForm", householderViewModel);
        }

        public ActionResult Delete(int id)
        {
            var householderInDb = _context.Householders.Include(h => h.Visits).Single(h => h.Id == id);

            while(householderInDb.Visits.Count > 0)
            {
                householderInDb.Visits.RemoveAt(0);
            }

            _context.Householders.Remove(householderInDb);

            _context.SaveChanges();

            return View();
        }

        public ActionResult ListVisits(int id)
        {
            var visits = _context.Householders.Include(h => h.Visits).Single(h => h.Id == id).Visits.ToList();

            return View(visits);
        }
    }
}