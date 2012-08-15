﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Xml;
using System.Text;

namespace Recurly
{
    public class Coupon
    {

        public enum CouponState : short
        {
            Redeemable,
            Expired,
            Maxed_Out
        }

        public enum CouponDiscountType
        {
            Percent,
            Dollars
        }

        public List<CouponRedemption> Redemptions { get; private set; }

        public string CouponCode { get; set; }
        public string Name { get; set; }
        public string HostedDescription { get; set; }
        public string InvoiceDescription { get; set; }
        public DateTime RedeemByDate { get; set; }
        public bool? SingleUse { get; set; }
        public int? AppliesForMonths { get; set; }
        public int? MaxRedemptions { get; set; }
        public bool? AppliesToAllPlans { get; set; }

        public CouponDiscountType DiscountType { get; private set; }
        public CouponState State { get; private set; }

        /// <summary>
        /// A dictionary of currencies and discounts
        /// </summary>
        public Dictionary<string, int> DiscountInCents { get; private set; }
        public int? DiscountPercent { get; private set; }

        /// <summary>
        /// A list of plans to limit the coupon
        /// </summary>
        public List<Plan> Plans
        {
            get
            {
                if (_plans == null)
                {
                    _plans = new List<Plan>();
                    foreach (string planCode in _planCodes)
                    {
                        _plans.Add(Plan.Get(planCode));
                    }
                }
                return _plans;

            }
            set{
                foreach (Plan p in value)
                {
                    _planCodes.Add(p.PlanCode);
                }
            }
        }

        /// <summary>
        /// When loading a coupon we get plan codes
        /// </summary>
        private List<string> _planCodes;
        private List<Plan> _plans;

        public DateTime CreatedAt { get; private set; }


        #region Constructors

        internal Coupon()
        {
        }

        internal Coupon(XmlTextReader xmlReader)
        {
            ReadXml(xmlReader);
        }

        /// <summary>
        /// Creates a coupon, discounted by a fixed amount
        /// </summary>
        /// <param name="couponCode"></param>
        /// <param name="name"></param>
        /// <param name="discountInCents">dictionary of currencies and discounts</param>
        public Coupon(string couponCode, string name, Dictionary<string, int> discountInCents)
        {
            this.CouponCode = couponCode;
            this.Name = name;
            this.DiscountInCents = discountInCents;
            this.DiscountType = CouponDiscountType.Dollars;
        }

        /// <summary>
        /// Creates a coupon, discounted by percentage
        /// </summary>
        /// <param name="couponCode"></param>
        /// <param name="name"></param>
        /// <param name="discountPercent"></param>
        public Coupon(string couponCode, string name, int discountPercent)
        {
            this.CouponCode = couponCode;
            this.Name = name;
            this.DiscountPercent = discountPercent;
            this.DiscountType = CouponDiscountType.Percent;
        }

        #endregion

        private const string UrlPrefix = "/coupons/";

        /// <summary>
        /// Look up a coupon
        /// </summary>
        /// <param name="couponCode">Coupon code</param>
        /// <returns></returns>
        public static Coupon Get(string couponCode)
        {
            Coupon coupon = new Coupon();

            HttpStatusCode statusCode = Client.PerformRequest(Client.HttpRequestMethod.Get,
                UrlPrefix + System.Web.HttpUtility.UrlEncode(couponCode),
                new Client.ReadXmlDelegate(coupon.ReadXml)).StatusCode;

            if (statusCode == HttpStatusCode.NotFound)
                return null;

            return coupon;
        }

        

        /// <summary>
        /// Deactivates this coupon.
        /// </summary>
        public void Deactivate()
        {
            Client.PerformRequest(Client.HttpRequestMethod.Delete, UrlPrefix + System.Web.HttpUtility.UrlEncode(this.CouponCode));
        }

        #region Read and Write XML documents

        internal void ReadXml(XmlTextReader reader)
        {
            DateTime date;
            while (reader.Read())
            {
                // End of coupon element, get out of here
                if (reader.Name == "coupon" && reader.NodeType == XmlNodeType.EndElement)
                    break;

                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "coupon_code":
                            this.CouponCode = reader.ReadElementContentAsString();
                            break;
                     
                        case "name":
                            this.Name = reader.ReadElementContentAsString();
                            break;

                        case "state":
                            this.State = (CouponState)Enum.Parse(typeof(CouponState), reader.ReadElementContentAsString(), true);
                            break;

                        case "discount_type":
                            this.DiscountType = (CouponDiscountType)Enum.Parse(typeof(CouponDiscountType), reader.ReadElementContentAsString(), true);
                            break;

                        case "discount_percent":
                            this.DiscountPercent = reader.ReadElementContentAsInt();
                            break;

                        case "redeem_by_date":
                            if (DateTime.TryParse(reader.ReadElementContentAsString(), out date))
                                this.RedeemByDate = date;
                            break;

                        case "single_use":
                            this.SingleUse = reader.ReadElementContentAsBoolean();
                            break;

                        case "applies_for_months":
                            this.AppliesForMonths = reader.ReadElementContentAsInt();
                            break;

                        case "max_redemptions":
                            this.MaxRedemptions = reader.ReadElementContentAsInt();
                            break;

                        case "applies_to_all_plans":
                            this.AppliesToAllPlans = reader.ReadElementContentAsBoolean();
                            break;

                        case "created_at":
                            if (DateTime.TryParse(reader.ReadElementContentAsString(), out date))
                                this.CreatedAt = date;
                            break;

                        case "plan_codes":
                            ReadXmlPlanCodes(reader);
                            break;

                        case "discount_in_cents":
                            ReadXmlDiscounts(reader);
                            break;
                        
                    }
                }
            }
        }


        internal void ReadXmlPlanCodes(XmlTextReader reader)
        {
            if (_planCodes == null)
                _planCodes = new List<string>();

            while (reader.Read())
            {
                if (reader.Name == "plan_codes" && reader.NodeType == XmlNodeType.EndElement)
                    break;

                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "plan_code":
                            this._planCodes.Add(reader.ReadElementContentAsString());
                            break;

                    }
                }
            }
        }

        internal void ReadXmlDiscounts(XmlTextReader reader)
        {
            if (this.DiscountInCents == null)
                this.DiscountInCents = new Dictionary<string, int>();

            while (reader.Read())
            {
                if (reader.Name == "discount_in_cents" && reader.NodeType == XmlNodeType.EndElement)
                    break;

                if (reader.NodeType == XmlNodeType.Element)
                {
                    this.DiscountInCents.Add(reader.Name, reader.ReadElementContentAsInt());
                }
            }
        }

        internal void WriteXml(XmlTextWriter xmlWriter)
        {
            xmlWriter.WriteStartElement("coupon"); // Start: coupon

            xmlWriter.WriteElementString("coupon_code", this.CouponCode);
            xmlWriter.WriteElementString("name", this.Name);
            xmlWriter.WriteElementString("hosted_description", this.HostedDescription);
            xmlWriter.WriteElementString("invoice_description", this.InvoiceDescription);
            if (null != this.RedeemByDate)
                xmlWriter.WriteElementString("redeem_by_date", this.RedeemByDate.ToString("s"));

            if (SingleUse.HasValue)
                xmlWriter.WriteElementString("single_use", this.SingleUse.Value.ToString());

            if (AppliesForMonths.HasValue)
                xmlWriter.WriteElementString("applies_for_months", AppliesForMonths.Value.ToString());

            if (AppliesToAllPlans.HasValue)
                xmlWriter.WriteElementString("applies_to_all_plans", AppliesToAllPlans.Value.ToString());

            xmlWriter.WriteElementString("discount_type", DiscountType.ToString().ToLower());

            if (CouponDiscountType.Percent == DiscountType && DiscountPercent.HasValue)
                xmlWriter.WriteElementString("discount_percent", DiscountPercent.Value.ToString());

            if (CouponDiscountType.Dollars == DiscountType && null != DiscountInCents)
            {
                xmlWriter.WriteStartElement("discount_in_cents");
                foreach(KeyValuePair<string, int> d in DiscountInCents)
                {
                    xmlWriter.WriteElementString(d.Key, d.Value.ToString());
                }
                xmlWriter.WriteEndElement();
            }


            xmlWriter.WriteEndElement(); // End: coupon
        }

        #endregion

        #region Object Overrides

        public override string ToString()
        {
            return "Recurly Account Coupon: " + this.CouponCode;
        }

        public override bool Equals(object obj)
        {
            if (obj is Coupon)
                return Equals((Coupon)obj);
            else
                return false;
        }

        public bool Equals(Coupon coupon)
        {
            return this.CouponCode == coupon.CouponCode;
        }

        public override int GetHashCode()
        {
            return this.CouponCode.GetHashCode();
        }

        #endregion
    }
}
