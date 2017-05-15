﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BigCommerceAccess.Misc;
using BigCommerceAccess.Models.Address;
using BigCommerceAccess.Models.Command;
using BigCommerceAccess.Models.Configuration;
using BigCommerceAccess.Models.Order;
using BigCommerceAccess.Services;
using CuttingEdge.Conditions;
using Netco.Extensions;

namespace BigCommerceAccess
{
	public class BigCommerceOrdersService: BigCommerceServiceBase, IBigCommerceOrdersService
	{
		private readonly WebRequestServices _webRequestServices;

		public BigCommerceOrdersService( BigCommerceConfig config )
		{
			Condition.Requires( config, "config" ).IsNotNull();
			this._webRequestServices = new WebRequestServices( config, this.GetMarker() );
		}

		#region Orders
		public List< BigCommerceOrder > GetOrders( DateTime dateFrom, DateTime dateTo )
		{
			var mainEndpoint = ParamsBuilder.CreateOrdersParams( dateFrom, dateTo );
			var orders = new List< BigCommerceOrder >();
			var marker = this.GetMarker();

			for( var i = 1; i < int.MaxValue; i++ )
			{
				var compositeEndpoint = mainEndpoint.ConcatParams( ParamsBuilder.CreateGetNextPageParams( new BigCommerceCommandConfig( i, RequestMaxLimit ) ) );
				var ordersWithinPage = ActionPolicies.Get.Get( () =>
					this._webRequestServices.GetResponse< List< BigCommerceOrder > >( BigCommerceCommand.GetOrders, compositeEndpoint, marker ) );
				this.CreateApiDelay( ordersWithinPage.Limits ).Wait(); //API requirement

				if( ordersWithinPage.Response == null )
					break;

				this.GetOrdersProducts( ordersWithinPage.Response, marker );
				this.GetOrdersShippingAddresses( ordersWithinPage.Response, marker );
				orders.AddRange( ordersWithinPage.Response );
				if( ordersWithinPage.Response.Count < RequestMaxLimit )
					break;
			}

			return orders;
		}

		public async Task< List< BigCommerceOrder > > GetOrdersAsync( DateTime dateFrom, DateTime dateTo, CancellationToken token )
		{
			var mainEndpoint = ParamsBuilder.CreateOrdersParams( dateFrom, dateTo );
			var orders = new List< BigCommerceOrder >();
			var marker = this.GetMarker();

			for( var i = 1; i < int.MaxValue; i++ )
			{
				var compositeEndpoint = mainEndpoint.ConcatParams( ParamsBuilder.CreateGetNextPageParams( new BigCommerceCommandConfig( i, RequestMaxLimit ) ) );
				var ordersWithinPage = await ActionPolicies.GetAsync.Get( async () =>
					await this._webRequestServices.GetResponseAsync< List< BigCommerceOrder > >( BigCommerceCommand.GetOrders, compositeEndpoint, marker ) );
				await this.CreateApiDelay( ordersWithinPage.Limits, token ); //API requirement

				if( ordersWithinPage.Response == null )
					break;

				await this.GetOrdersProductsAsync( ordersWithinPage.Response, ordersWithinPage.Limits.IsUnlimitedCallsCount, token, marker );
				await this.GetOrdersShippingAddressesAsync( ordersWithinPage.Response, ordersWithinPage.Limits.IsUnlimitedCallsCount, token, marker );
				orders.AddRange( ordersWithinPage.Response );
				if( ordersWithinPage.Response.Count < RequestMaxLimit )
					break;
			}

			return orders;
		}
		#endregion

		#region Order products
		private void GetOrdersProducts( IEnumerable< BigCommerceOrder > orders, string marker )
		{
			foreach( var order in orders )
			{
				for( var i = 1; i < int.MaxValue; i++ )
				{
					var endpoint = ParamsBuilder.CreateGetNextPageParams( new BigCommerceCommandConfig( i, RequestMaxLimit ) );
					var products = ActionPolicies.Get.Get( () =>
						this._webRequestServices.GetResponse< List< BigCommerceOrderProduct > >( order.ProductsReference.Url, endpoint, marker ) );
					this.CreateApiDelay( products.Limits ).Wait(); //API requirement

					if( products.Response == null )
						break;
					order.Products.AddRange( products.Response );
					if( products.Response.Count < RequestMaxLimit )
						break;
				}
			}
		}

		private async Task GetOrdersProductsAsync( IEnumerable< BigCommerceOrder > orders, bool isUnlimit, CancellationToken token, string marker )
		{
			var threadCount = isUnlimit ? MaxThreadsCount : 1;
			await orders.DoInBatchAsync( threadCount, async order =>
			{
				for( var i = 1; i < int.MaxValue; i++ )
				{
					var endpoint = ParamsBuilder.CreateGetNextPageParams( new BigCommerceCommandConfig( i, RequestMaxLimit ) );
					var products = await ActionPolicies.GetAsync.Get( async () =>
						await this._webRequestServices.GetResponseAsync< List< BigCommerceOrderProduct > >( order.ProductsReference.Url, endpoint, marker ) );
					await this.CreateApiDelay( products.Limits, token ); //API requirement

					if( products.Response == null )
						break;
					order.Products.AddRange( products.Response );
					if( products.Response.Count < RequestMaxLimit )
						break;
				}
			} );
		}
		#endregion

		#region ShippingAddress
		private void GetOrdersShippingAddresses( IEnumerable< BigCommerceOrder > orders, string marker )
		{
			foreach( var order in orders )
			{
				var addresses = ActionPolicies.Get.Get( () =>
					this._webRequestServices.GetResponse< List< BigCommerceShippingAddress > >( order.ShippingAddressesReference.Url, marker ) );
				order.ShippingAddresses = addresses.Response;
				this.CreateApiDelay( addresses.Limits ).Wait(); //API requirement
			}
		}

		private async Task GetOrdersShippingAddressesAsync( IEnumerable< BigCommerceOrder > orders, bool isUnlimit, CancellationToken token, string marker )
		{
			var threadCount = isUnlimit ? MaxThreadsCount : 1;
			await orders.DoInBatchAsync( threadCount, async order =>
			{
				var addresses = await ActionPolicies.GetAsync.Get( async () =>
					await this._webRequestServices.GetResponseAsync< List< BigCommerceShippingAddress > >( order.ShippingAddressesReference.Url, marker ) );
				order.ShippingAddresses = addresses.Response;
				await this.CreateApiDelay( addresses.Limits, token ); //API requirement
			} );
		}
		#endregion
	}
}