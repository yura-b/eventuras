using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Eventuras.Domain;
using Eventuras.Services;
using Eventuras.TestAbstractions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using NodaTime;
using Xunit;

namespace Eventuras.WebApi.Tests.Controllers.Registrations
{
    public class RegistrationsControllerTest : IClassFixture<CustomWebApiApplicationFactory<Startup>>
    {
        private readonly CustomWebApiApplicationFactory<Startup> _factory;

        public RegistrationsControllerTest(CustomWebApiApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Should_Return_Unauthorized_For_Not_Logged_In_User()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/v3/registrations");
            response.CheckUnauthorized();
        }

        [Fact]
        public async Task Should_Return_Empty_Registration_List()
        {
            var client = _factory.CreateClient()
                .Authenticated();

            var response = await client.GetAsync("/v3/registrations");
            var paging = await response.AsTokenAsync();
            paging.CheckEmptyPaging();
        }

        [Theory]
        [InlineData("test", null)]
        [InlineData(-1, null)]
        [InlineData(0, null)]
        [InlineData(null, "test")]
        [InlineData(null, -1)]
        [InlineData(null, 101)]
        public async Task Should_Return_BadRequest_For_Invalid_Paging_Params(object page, object count)
        {
            var client = _factory.CreateClient().Authenticated();

            var q = new List<object>();
            if (page != null)
            {
                q.Add($"page={WebUtility.UrlEncode(page.ToString())}");
            }

            if (count != null)
            {
                q.Add($"count={WebUtility.UrlEncode(count.ToString())}");
            }

            var response = await client.GetAsync("/v3/registrations?" + string.Join("&", q));
            response.CheckBadRequest();
        }

        [Fact]
        public async Task Should_Use_Paging_For_Registration_List()
        {
            var client = _factory.CreateClient()
                .AuthenticatedAsSystemAdmin();

            using var scope = _factory.Services.NewTestScope();
            using var e = await scope.CreateEventAsync();
            using var user = await scope.CreateUserAsync();
            using var r1 = await scope.CreateRegistrationAsync(e.Entity, user.Entity,
                time: SystemClock.Instance.Now().Plus(Duration.FromDays(3)));
            using var r2 = await scope.CreateRegistrationAsync(e.Entity, user.Entity,
                time: SystemClock.Instance.Now().Plus(Duration.FromDays(2)));
            using var r3 = await scope.CreateRegistrationAsync(e.Entity, user.Entity,
                time: SystemClock.Instance.Now().Plus(Duration.FromDays(1)));

            var response = await client.GetAsync("/v3/registrations?page=1&count=2");
            var paging = await response.AsTokenAsync();
            paging.CheckPaging(1, 2, 3,
                (token, r) => token.CheckRegistration(r),
                r1.Entity, r2.Entity);

            response = await client.GetAsync("/v3/registrations?page=2&count=2");
            paging = await response.AsTokenAsync();
            paging.CheckPaging(2, 2, 3,
                (token, r) => token.CheckRegistration(r),
                r3.Entity);
        }

        [Fact]
        public async Task Should_Limit_Registrations_For_Regular_User()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var otherUser = await scope.CreateUserAsync();

            using var e = await scope.CreateEventAsync();
            using var r1 = await scope.CreateRegistrationAsync(e.Entity, user.Entity,
                time: SystemClock.Instance.Now().Plus(Duration.FromDays(3)));
            using var r2 = await scope.CreateRegistrationAsync(e.Entity, user.Entity,
                time: SystemClock.Instance.Now().Plus(Duration.FromDays(2)));
            using var r3 =
                await scope.CreateRegistrationAsync(e.Entity, otherUser.Entity,
                    time: SystemClock.Instance.Now().Plus(Duration.FromDays(1)));

            var client = _factory.CreateClient()
                .AuthenticatedAs(user.Entity);

            var response = await client.GetAsync("/v3/registrations");
            var paging = await response.AsTokenAsync();
            paging.CheckPaging(1, 2,
                (token, r) => token.CheckRegistration(r),
                r1.Entity, r2.Entity);

            client.AuthenticatedAs(otherUser.Entity);

            response = await client.GetAsync("/v3/registrations");
            paging = await response.AsTokenAsync();
            paging.CheckPaging(1, 1,
                (token, r) => token.CheckRegistration(r),
                r3.Entity);
        }

        [Fact]
        public async Task Should_Limit_Registrations_For_Admin()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var otherUser = await scope.CreateUserAsync();
            using var admin = await scope.CreateUserAsync();
            using var otherAdmin = await scope.CreateUserAsync();
            using var org = await scope.CreateOrganizationAsync();
            using var otherOrg = await scope.CreateOrganizationAsync();

            await scope.CreateOrganizationMemberAsync(admin.Entity, org.Entity, role: Roles.Admin);
            await scope.CreateOrganizationMemberAsync(otherAdmin.Entity, otherOrg.Entity, role: Roles.Admin);

            using var e1 =
                await scope.CreateEventAsync(organization: org.Entity, organizationId: org.Entity.OrganizationId);
            using var e2 = await scope.CreateEventAsync(organization: otherOrg.Entity,
                organizationId: otherOrg.Entity.OrganizationId);
            using var r1 = await scope.CreateRegistrationAsync(e1.Entity, user.Entity,
                time: SystemClock.Instance.Now().Plus(Duration.FromDays(3)));
            using var r2 =
                await scope.CreateRegistrationAsync(e1.Entity, otherUser.Entity,
                    time: SystemClock.Instance.Now().Plus(Duration.FromDays(2)));
            using var r3 =
                await scope.CreateRegistrationAsync(e2.Entity, otherUser.Entity,
                    time: SystemClock.Instance.Now().Plus(Duration.FromDays(1)));

            var client = _factory.CreateClient()
                .AuthenticatedAs(admin.Entity, Roles.Admin);

            var response = await client.GetAsync($"/v3/registrations?orgId={org.Entity.OrganizationId}");
            var paging = await response.AsTokenAsync();
            paging.CheckPaging(1, 2,
                (token, r) => token.CheckRegistration(r),
                r1.Entity, r2.Entity);

            client.AuthenticatedAs(otherAdmin.Entity, Roles.Admin);

            response = await client.GetAsync($"/v3/registrations?orgId={org.Entity.OrganizationId}");
            paging = await response.AsTokenAsync();
            paging.CheckEmptyPaging();
        }

        [Fact]
        public async Task Should_Not_Limit_Registrations_For_System_Admin()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var otherUser = await scope.CreateUserAsync();

            using var e = await scope.CreateEventAsync();
            using var r1 = await scope.CreateRegistrationAsync(e.Entity, user.Entity,
                time: SystemClock.Instance.Now().Plus(Duration.FromDays(3)));
            using var r2 = await scope.CreateRegistrationAsync(e.Entity, user.Entity,
                time: SystemClock.Instance.Now().Plus(Duration.FromDays(2)));
            using var r3 =
                await scope.CreateRegistrationAsync(e.Entity, otherUser.Entity,
                    time: SystemClock.Instance.Now().Plus(Duration.FromDays(1)));

            var client = _factory.CreateClient()
                .AuthenticatedAsSystemAdmin();

            var response = await client.GetAsync("/v3/registrations");
            var paging = await response.AsTokenAsync();
            paging.CheckPaging(1, 3,
                (token, r) => token.CheckRegistration(r),
                r1.Entity, r2.Entity, r3.Entity);
        }

        [Fact]
        public async Task Should_Not_Include_User_And_Event_Info_By_Default()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var otherUser = await scope.CreateUserAsync();

            using var evt = await scope.CreateEventAsync();
            using var reg = await scope.CreateRegistrationAsync(evt.Entity, user.Entity,
                time: SystemClock.Instance.Now().Plus(Duration.FromDays(3)));

            var client = _factory.CreateClient()
                .AuthenticatedAsSystemAdmin();

            var response = await client.GetAsync("/v3/registrations");
            var paging = await response.AsTokenAsync();
            paging.CheckPaging((token, r) =>
            {
                token.CheckRegistration(r);
                Assert.Empty(token.Value<JToken>("user"));
                Assert.Empty(token.Value<JToken>("event"));
            }, reg.Entity);
        }

        [Fact]
        public async Task Should_Use_IncludeUserInfo_Param()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var otherUser = await scope.CreateUserAsync();

            using var evt = await scope.CreateEventAsync();
            using var reg = await scope.CreateRegistrationAsync(evt.Entity, user.Entity,
                time: SystemClock.Instance.Now().Plus(Duration.FromDays(3)));

            var client = _factory.CreateClient()
                .AuthenticatedAsSystemAdmin();

            var response = await client.GetAsync("/v3/registrations?includeUserInfo=true");
            var paging = await response.AsTokenAsync();
            paging.CheckPaging((token, r) =>
            {
                token.CheckRegistration(r, checkUserInfo: true);
                Assert.NotEmpty(token.Value<JToken>("user"));
                Assert.Empty(token.Value<JToken>("event"));
            }, reg.Entity);
        }

        [Fact]
        public async Task Should_Use_IncludeEventInfo_Param()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var otherUser = await scope.CreateUserAsync();

            using var evt = await scope.CreateEventAsync();
            using var reg = await scope.CreateRegistrationAsync(evt.Entity, user.Entity,
                time: SystemClock.Instance.Now().Plus(Duration.FromDays(3)));

            var client = _factory.CreateClient()
                .AuthenticatedAsSystemAdmin();

            var response = await client.GetAsync("/v3/registrations?includeEventInfo=true");
            var paging = await response.AsTokenAsync();
            paging.CheckPaging((token, r) =>
            {
                token.CheckRegistration(r, checkUserInfo: false, checkEventInfo: true);
                Assert.NotEmpty(token.Value<JToken>("event"));
                Assert.Empty(token.Value<JToken>("user"));
            }, reg.Entity);
        }


        [Fact]
        public async Task Should_Require_Auth_For_Creating_New_Reg()
        {
            var client = _factory.CreateClient();
            var response = await client.PostAsync("/v3/registrations", new
            {
                userId = Guid.NewGuid(),
                eventId = Guid.NewGuid()
            });
            response.CheckUnauthorized();
        }

        [Fact]
        public async Task Should_Return_Not_Found_For_Unknown_User_Id()
        {
            var client = _factory.CreateClient().Authenticated();
            using var scope = _factory.Services.NewTestScope();
            using var e = await scope.CreateEventAsync();
            var response = await client.PostAsync("/v3/registrations", new
            {
                userId = Guid.NewGuid(),
                eventId = e.Entity.EventInfoId
            });
            response.CheckNotFound();
        }

        [Fact]
        public async Task Should_Return_Not_Found_For_Unknown_Event_Id()
        {
            var client = _factory.CreateClient().Authenticated();
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            var response = await client.PostAsync("/v3/registrations", new
            {
                userId = user.Entity.Id,
                eventId = 1001
            });
            response.CheckNotFound();
        }

        [Fact]
        public async Task Should_Require_Event_Id_For_Creating_New_Reg()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();

            var client = _factory.CreateClient().AuthenticatedAs(user.Entity);
            var response = await client.PostAsync("/v3/registrations", new { userId = user.Entity.Id });
            response.CheckBadRequest();
        }

        [Fact]
        public async Task Should_Require_User_Id_For_Creating_New_Reg()
        {
            var client = _factory.CreateClient().Authenticated();
            var response = await client.PostAsync("/v3/registrations", new { eventId = 10001 });
            response.CheckBadRequest();
        }

        [Fact]
        public async Task Should_Not_Allow_Admin_To_Create_Reg_For_Inaccessible_Event()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var admin = await scope.CreateUserAsync(role: Roles.Admin);
            using var org = await scope.CreateOrganizationAsync();
            using var e = await scope.CreateEventAsync(organization: org.Entity);

            var client = _factory.CreateClient().AuthenticatedAs(admin.Entity, Roles.Admin);
            var response = await client.PostAsync($"/v3/registrations?orgId={org.Entity.OrganizationId}", new
            {
                userId = user.Entity.Id,
                eventId = e.Entity.EventInfoId
            });
            response.CheckForbidden();
        }

        [Fact]
        public async Task Should_Allow_Regular_User_To_Register_To_Event_Open_For_Registrations()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var e = await scope.CreateEventAsync(status: EventInfo.EventInfoStatus.RegistrationsOpen);

            var client = _factory.CreateClient().AuthenticatedAs(user.Entity);
            var response = await client.PostAsync("/v3/registrations", new
            {
                userId = user.Entity.Id,
                eventId = e.Entity.EventInfoId
            });
            response.CheckOk();

            var reg = await scope.Db.Registrations.SingleAsync(r =>
                r.EventInfoId == e.Entity.EventInfoId &&
                r.UserId == user.Entity.Id);

            var token = await response.AsTokenAsync();
            token.CheckRegistration(reg);
        }

        [Fact]
        public async Task Should_Allow_Regular_User_To_Register_Via_Alias_Endpoint()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var e = await scope.CreateEventAsync(status: EventInfo.EventInfoStatus.RegistrationsOpen);

            var client = _factory.CreateClient().AuthenticatedAs(user.Entity);
            var response = await client.PostAsync($"/v3/registrations/me/{e.Entity.EventInfoId}");
            response.CheckOk();

            var reg = await scope.Db.Registrations
                .SingleAsync(r =>
                    r.EventInfoId == e.Entity.EventInfoId &&
                    r.UserId == user.Entity.Id);

            var token = await response.AsTokenAsync();
            token.CheckRegistration(reg);
        }

        [Fact]
        public async Task Should_Not_Create_Order_When_Using_Alias_Endpoint_With_No_Params()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var e = await scope.CreateEventAsync(status: EventInfo.EventInfoStatus.RegistrationsOpen);
            using var mandatoryProduct = await scope.CreateProductAsync(e.Entity, minimumQuantity: 1);

            var client = _factory.CreateClient().AuthenticatedAs(user.Entity);
            var response = await client.PostAsync($"/v3/registrations/me/{e.Entity.EventInfoId}");
            response.CheckOk();

            var reg = await scope.Db.Registrations
                .Include(r => r.Orders)
                .SingleAsync(r =>
                    r.EventInfoId == e.Entity.EventInfoId &&
                    r.UserId == user.Entity.Id);

            var token = await response.AsTokenAsync();
            token.CheckRegistration(reg);
            Assert.Empty(reg.Orders);
        }

        [Fact]
        public async Task Self_Registration_Alias_Endpoint_Should_Support_CreateOrder_Param()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var e = await scope.CreateEventAsync(status: EventInfo.EventInfoStatus.RegistrationsOpen);
            using var mandatoryProduct = await scope.CreateProductAsync(e.Entity, minimumQuantity: 2);
            using var nonMandatoryProduct = await scope.CreateProductAsync(e.Entity, minimumQuantity: 0);

            var client = _factory.CreateClient().AuthenticatedAs(user.Entity);
            var response = await client.PostAsync($"/v3/registrations/me/{e.Entity.EventInfoId}?createOrder=true");
            response.CheckOk();

            var reg = await scope.Db.Registrations
                .Include(r => r.Orders)
                .ThenInclude(o => o.OrderLines)
                .SingleAsync(r =>
                    r.EventInfoId == e.Entity.EventInfoId &&
                    r.UserId == user.Entity.Id);

            var token = await response.AsTokenAsync();
            token.CheckRegistration(reg);
            Assert.Single(reg.Orders);

            var order = reg.Orders.Single();
            Assert.Single(order.OrderLines);

            var line = order.OrderLines.Single();
            Assert.Equal(mandatoryProduct.Entity.ProductId, line.ProductId);
            Assert.Equal(mandatoryProduct.Entity.MinimumQuantity, line.Quantity);
        }

        [Fact]
        public async Task Should_Not_Allow_Regular_User_To_Register_To_Event_Not_Open_For_Registrations()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var e = await scope.CreateEventAsync(status: EventInfo.EventInfoStatus.Planned);

            var client = _factory.CreateClient().AuthenticatedAs(user.Entity);
            var response = await client.PostAsync("/v3/registrations", new
            {
                userId = user.Entity.Id,
                eventId = e.Entity.EventInfoId
            });
            response.CheckForbidden();
        }

        [Fact]
        public async Task Should_Not_Create_Order_By_Default()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var e = await scope.CreateEventAsync(status: EventInfo.EventInfoStatus.RegistrationsOpen);
            using var mandatoryProduct = await scope.CreateProductAsync(e.Entity, minimumQuantity: 1);

            var client = _factory.CreateClient().AuthenticatedAs(user.Entity);
            var response = await client.PostAsync("/v3/registrations", new
            {
                userId = user.Entity.Id,
                eventId = e.Entity.EventInfoId
            });
            response.CheckOk();

            var reg = await scope.Db.Registrations
                .Include(r => r.Orders)
                .SingleAsync(r =>
                    r.EventInfoId == e.Entity.EventInfoId &&
                    r.UserId == user.Entity.Id);

            var token = await response.AsTokenAsync();
            token.CheckRegistration(reg);
            Assert.Empty(reg.Orders);
        }

        [Fact]
        public async Task Should_Move_Event_To_Waiting_List_Status()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var evt = await scope.CreateEventAsync(
                status: EventInfo.EventInfoStatus.RegistrationsOpen,
                maxParticipants: 1);
            using var mandatoryProduct = await scope.CreateProductAsync(evt.Entity, minimumQuantity: 1);

            var client = _factory.CreateClient().AuthenticatedAs(user.Entity);
            var response = await client.PostAsync("/v3/registrations", new
            {
                userId = user.Entity.Id,
                eventId = evt.Entity.EventInfoId,
                createOrder = true
            });
            response.CheckOk();

            var reg = await scope.Db.Registrations
                .Include(r => r.Orders)
                .SingleAsync(r =>
                    r.EventInfoId == evt.Entity.EventInfoId &&
                    r.UserId == user.Entity.Id);

            var token = await response.AsTokenAsync();
            token.CheckRegistration(reg);
            Assert.NotEmpty(reg.Orders);
            Assert.Equal(Registration.RegistrationStatus.Draft, reg.Status);

            var updatedEvent = await scope.Db.EventInfos
                .AsNoTracking()
                .SingleAsync(e => e.EventInfoId == evt.Entity.EventInfoId);
            Assert.Equal(EventInfo.EventInfoStatus.WaitingList, updatedEvent.Status);
        }

        [Fact]
        public async Task Should_Handle_Waiting_List_Status()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var e = await scope.CreateEventAsync(status: EventInfo.EventInfoStatus.WaitingList);
            using var mandatoryProduct = await scope.CreateProductAsync(e.Entity, minimumQuantity: 1);

            var client = _factory.CreateClient().AuthenticatedAs(user.Entity);
            var response = await client.PostAsync("/v3/registrations", new
            {
                userId = user.Entity.Id,
                eventId = e.Entity.EventInfoId,
                createOrder = true
            });
            response.CheckOk();

            var reg = await scope.Db.Registrations
                .Include(r => r.Orders)
                .SingleAsync(r =>
                    r.EventInfoId == e.Entity.EventInfoId &&
                    r.UserId == user.Entity.Id);

            var token = await response.AsTokenAsync();
            token.CheckRegistration(reg);
            Assert.Empty(reg.Orders);
            Assert.Equal(Registration.RegistrationStatus.WaitingList, reg.Status);
        }

        [Fact]
        public async Task Should_Use_CreateOrder_Param_When_Creating_New_Reg()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var e = await scope.CreateEventAsync(status: EventInfo.EventInfoStatus.RegistrationsOpen);
            using var mandatoryProduct = await scope.CreateProductAsync(e.Entity, minimumQuantity: 2);
            using var nonMandatoryProduct = await scope.CreateProductAsync(e.Entity, minimumQuantity: 0);

            var client = _factory.CreateClient().AuthenticatedAs(user.Entity);
            var response = await client.PostAsync("/v3/registrations", new
            {
                userId = user.Entity.Id,
                eventId = e.Entity.EventInfoId,
                createOrder = true
            });
            response.CheckOk();

            var reg = await scope.Db.Registrations
                .Include(r => r.Orders)
                .ThenInclude(o => o.OrderLines)
                .SingleAsync(r =>
                    r.EventInfoId == e.Entity.EventInfoId &&
                    r.UserId == user.Entity.Id);

            var token = await response.AsTokenAsync();
            token.CheckRegistration(reg);
            Assert.Single(reg.Orders);

            var order = reg.Orders.Single();
            Assert.Single(order.OrderLines);

            var orderLine = order.OrderLines.First();
            Assert.Equal(mandatoryProduct.Entity.ProductId, orderLine.ProductId);
            Assert.Equal(mandatoryProduct.Entity.MinimumQuantity, orderLine.Quantity);
        }

        [Theory]
        [MemberData(nameof(GetRegInfoWithAdditionalInfoFilled))]
        public async Task Should_Not_Allow_Regular_User_To_Provide_Extra_Info(Func<string, int, object> f,
            Action<Registration> _)
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var e = await scope.CreateEventAsync();

            var client = _factory.CreateClient().AuthenticatedAs(user.Entity);
            var response = await client.PostAsync("/v3/registrations", f(user.Entity.Id, e.Entity.EventInfoId));
            response.CheckBadRequest();
        }

        [Fact]
        public async Task Should_Allow_Admin_To_Create_Reg_For_Accessible_Event()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var admin = await scope.CreateUserAsync(role: Roles.Admin);
            using var org = await scope.CreateOrganizationAsync();
            using var member = await scope.CreateOrganizationMemberAsync(admin.Entity, org.Entity, role: Roles.Admin);
            using var e =
                await scope.CreateEventAsync(organization: org.Entity, organizationId: org.Entity.OrganizationId);

            var client = _factory.CreateClient().AuthenticatedAs(admin.Entity, Roles.Admin);
            var response = await client.PostAsync($"/v3/registrations?orgId={org.Entity.OrganizationId}", new
            {
                userId = user.Entity.Id,
                eventId = e.Entity.EventInfoId
            });
            response.CheckOk();

            var reg = await scope.Db.Registrations.SingleAsync(r =>
                r.EventInfoId == e.Entity.EventInfoId &&
                r.UserId == user.Entity.Id);

            var token = await response.AsTokenAsync();
            token.CheckRegistration(reg);
        }

        [Theory]
        [MemberData(nameof(GetRegInfoWithAdditionalInfoFilled))]
        public async Task Should_Allow_Admin_To_Provide_Extra_Info_When_Creating_New_Reg(Func<string, int, object> f,
            Action<Registration> check)
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var admin = await scope.CreateUserAsync(role: Roles.Admin);
            using var org = await scope.CreateOrganizationAsync();
            using var member = await scope.CreateOrganizationMemberAsync(admin.Entity, org.Entity, role: Roles.Admin);
            using var e =
                await scope.CreateEventAsync(organization: org.Entity, organizationId: org.Entity.OrganizationId);

            var client = _factory.CreateClient().AuthenticatedAs(admin.Entity, Roles.Admin);
            var response = await client.PostAsync($"/v3/registrations?orgId={org.Entity.OrganizationId}",
                f(user.Entity.Id, e.Entity.EventInfoId));
            response.CheckOk();

            var reg = await scope.Db.Registrations.SingleAsync(r =>
                r.EventInfoId == e.Entity.EventInfoId &&
                r.UserId == user.Entity.Id);

            var token = await response.AsTokenAsync();
            token.CheckRegistration(reg);
            check(reg);
        }

        [Fact]
        public async Task Should_Allow_System_Admin_To_Create_Reg_For_Any_Event()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var e = await scope.CreateEventAsync();

            var client = _factory.CreateClient().AuthenticatedAsSystemAdmin();
            var response = await client.PostAsync("/v3/registrations", new
            {
                userId = user.Entity.Id,
                eventId = e.Entity.EventInfoId
            });
            response.CheckOk();

            var reg = await scope.Db.Registrations.SingleAsync(r =>
                r.EventInfoId == e.Entity.EventInfoId &&
                r.UserId == user.Entity.Id);

            var token = await response.AsTokenAsync();
            token.CheckRegistration(reg);
        }

        [Theory]
        [MemberData(nameof(GetRegInfoWithAdditionalInfoFilled))]
        public async Task Should_Allow_System_Admin_To_Provide_Extra_Info(Func<string, int, object> f,
            Action<Registration> check)
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var e = await scope.CreateEventAsync();

            var client = _factory.CreateClient().AuthenticatedAsSystemAdmin();
            var response = await client.PostAsync("/v3/registrations", f(user.Entity.Id, e.Entity.EventInfoId));
            response.CheckOk();

            var reg = await scope.Db.Registrations.SingleAsync(r =>
                r.EventInfoId == e.Entity.EventInfoId &&
                r.UserId == user.Entity.Id);

            var token = await response.AsTokenAsync();
            token.CheckRegistration(reg);
            check(reg);
        }


        [Fact]
        public async Task Should_Require_Auth_For_Updating_Reg()
        {
            var client = _factory.CreateClient();
            var response = await client.PutAsync("/v3/registrations/1", new
            {
                type = 1
            });
            response.CheckUnauthorized();
        }

        [Fact]
        public async Task Should_Return_Not_Found_When_Updating_Unknown_Reg()
        {
            var client = _factory.CreateClient().AuthenticatedAsSystemAdmin();
            var response = await client.PutAsync("/v3/registrations/1", new
            {
                type = 1
            });
            response.CheckNotFound();
        }

        [Fact]
        public async Task Should_Not_Allow_Regular_User_To_Update_Own_Reg()
        {
            var client = _factory.CreateClient().Authenticated();
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var e = await scope.CreateEventAsync();
            using var registration = await scope.CreateRegistrationAsync(e.Entity, user.Entity);
            var response = await client.PutAsync($"/v3/registrations/{registration.Entity.RegistrationId}", new { });
            response.CheckForbidden();
        }

        [Fact]
        public async Task Should_Not_Allow_Admin_To_Update_Reg_For_Inaccessible_Event()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var admin = await scope.CreateUserAsync(role: Roles.Admin);
            using var org = await scope.CreateOrganizationAsync();
            using var e = await scope.CreateEventAsync(organization: org.Entity);
            using var registration = await scope.CreateRegistrationAsync(e.Entity, user.Entity);

            var client = _factory.CreateClient().AuthenticatedAs(admin.Entity, Roles.Admin);
            var response = await client.PutAsync(
                $"/v3/registrations/{registration.Entity.RegistrationId}?orgId={org.Entity.OrganizationId}", new
                {
                    type = 2
                });
            response.CheckForbidden();
        }

        [Fact]
        public async Task Should_Allow_SystemAdmin_To_Update_Reg_For_Accessible_Event()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var admin = await scope.CreateUserAsync(role: Roles.SystemAdmin);
            using var org = await scope.CreateOrganizationAsync();
            using var member = await scope.CreateOrganizationMemberAsync(admin.Entity, org.Entity, role: Roles.Admin);
            using var e = await scope.CreateEventAsync(organization: org.Entity);
            using var registration = await scope.CreateRegistrationAsync(e.Entity, user.Entity);
            Assert.Equal(Registration.RegistrationType.Participant, registration.Entity.Type);

            var client = _factory.CreateClient().AuthenticatedAs(admin.Entity, Roles.SystemAdmin);
            var response = await client.PutAsync(
                $"/v3/registrations/{registration.Entity.RegistrationId}?orgId={org.Entity.OrganizationId}", new
                {
                    type = 2
                });
            response.CheckOk();

            var updated = await LoadAndCheckAsync(scope, registration.Entity, updated =>
                Assert.Equal(Registration.RegistrationType.Staff, updated.Type));

            var token = await response.AsTokenAsync();
            token.CheckRegistration(updated);
        }

        [Fact(Skip = "Rewrite when organization admin is in place again.")]
        public async Task Should_Allow_Admin_To_Update_Reg_For_Accessible_Event()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var admin = await scope.CreateUserAsync(role: Roles.Admin);
            using var org = await scope.CreateOrganizationAsync();
            using var member = await scope.CreateOrganizationMemberAsync(admin.Entity, org.Entity, role: Roles.Admin);
            using var e = await scope.CreateEventAsync(organization: org.Entity);
            using var registration = await scope.CreateRegistrationAsync(e.Entity, user.Entity);
            Assert.Equal(Registration.RegistrationType.Participant, registration.Entity.Type);

            var client = _factory.CreateClient().AuthenticatedAs(admin.Entity, Roles.Admin);
            var response = await client.PutAsync(
                $"/v3/registrations/{registration.Entity.RegistrationId}?orgId={org.Entity.OrganizationId}", new
                {
                    type = 2
                });
            response.CheckOk();

            var updated = await LoadAndCheckAsync(scope, registration.Entity, updated =>
                Assert.Equal(Registration.RegistrationType.Staff, updated.Type));

            var token = await response.AsTokenAsync();
            token.CheckRegistration(updated);
        }

        [Fact]
        public async Task Should_Allow_System_Admin_To_Update_Any_Reg()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var e = await scope.CreateEventAsync();
            using var registration = await scope.CreateRegistrationAsync(e.Entity, user.Entity);

            var client = _factory.CreateClient().AuthenticatedAsSystemAdmin();
            var response = await client.PutAsync($"/v3/registrations/{registration.Entity.RegistrationId}", new
            {
                type = 2
            });
            response.CheckOk();

            var updated = await LoadAndCheckAsync(scope, registration.Entity, updated =>
                Assert.Equal(Registration.RegistrationType.Staff, updated.Type));

            var token = await response.AsTokenAsync();
            token.CheckRegistration(updated);
        }


        [Fact]
        public async Task Should_Require_Auth_For_Cancelling_Reg()
        {
            var client = _factory.CreateClient();
            var response = await client.DeleteAsync("/v3/registrations/1");
            response.CheckUnauthorized();
        }

        [Fact]
        public async Task Should_Return_Not_Found_When_Cancelling_Unknown_Reg()
        {
            var client = _factory.CreateClient().AuthenticatedAsSystemAdmin();
            var response = await client.DeleteAsync("/v3/registrations/1");
            response.CheckNotFound();
        }

        [Fact]
        public async Task Should_Not_Allow_Regular_User_To_Cancel_Own_Reg()
        {
            var client = _factory.CreateClient().Authenticated();
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var e = await scope.CreateEventAsync();
            using var registration = await scope.CreateRegistrationAsync(e.Entity, user.Entity);
            var response = await client.DeleteAsync($"/v3/registrations/{registration.Entity.RegistrationId}");
            response.CheckForbidden();
        }

        [Fact]
        public async Task Should_Not_Allow_Admin_To_Cancel_Reg_For_Inaccessible_Event()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var admin = await scope.CreateUserAsync(role: Roles.Admin);
            using var org = await scope.CreateOrganizationAsync();
            using var e = await scope.CreateEventAsync(organization: org.Entity);
            using var registration = await scope.CreateRegistrationAsync(e.Entity, user.Entity);

            var client = _factory.CreateClient().AuthenticatedAs(admin.Entity, Roles.Admin);
            var response =
                await client.DeleteAsync(
                    $"/v3/registrations/{registration.Entity.RegistrationId}?orgId={org.Entity.OrganizationId}");
            response.CheckForbidden();
        }

        [Fact]
        public async Task Should_Allow_Admin_To_Cancel_Reg_For_Accessible_Event()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var admin = await scope.CreateUserAsync(role: Roles.Admin);
            using var org = await scope.CreateOrganizationAsync();
            using var member = await scope.CreateOrganizationMemberAsync(admin.Entity, org.Entity, role: Roles.Admin);
            using var e =
                await scope.CreateEventAsync(organization: org.Entity, organizationId: org.Entity.OrganizationId);
            using var registration = await scope.CreateRegistrationAsync(e.Entity, user.Entity);
            Assert.Equal(Registration.RegistrationType.Participant, registration.Entity.Type);

            var client = _factory.CreateClient().AuthenticatedAs(admin.Entity, Roles.Admin);
            var response =
                await client.DeleteAsync(
                    $"/v3/registrations/{registration.Entity.RegistrationId}?orgId={org.Entity.OrganizationId}");
            response.CheckOk();

            await LoadAndCheckAsync(scope, registration.Entity, updated =>
                Assert.Equal(Registration.RegistrationStatus.Cancelled, updated.Status));
        }

        [Fact]
        public async Task Should_Allow_System_Admin_To_Cancel_Any_Reg()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var e = await scope.CreateEventAsync();
            using var registration = await scope.CreateRegistrationAsync(e.Entity, user.Entity);

            var client = _factory.CreateClient().AuthenticatedAsSystemAdmin();
            var response = await client.DeleteAsync($"/v3/registrations/{registration.Entity.RegistrationId}");
            response.CheckOk();

            await LoadAndCheckAsync(scope, registration.Entity, updated =>
                Assert.Equal(Registration.RegistrationStatus.Cancelled, updated.Status));
        }

        [Fact]
        public async Task Should_Allow_To_Cancel_Second_Time()
        {
            using var scope = _factory.Services.NewTestScope();
            using var user = await scope.CreateUserAsync();
            using var e = await scope.CreateEventAsync();
            using var registration =
                await scope.CreateRegistrationAsync(e.Entity, user.Entity, Registration.RegistrationStatus.Cancelled);

            var client = _factory.CreateClient().AuthenticatedAsSuperAdmin();
            var response = await client.DeleteAsync($"/v3/registrations/{registration.Entity.RegistrationId}");
            response.CheckOk();

            await LoadAndCheckAsync(scope, registration.Entity, updated =>
                Assert.Equal(Registration.RegistrationStatus.Cancelled, updated.Status));
        }

        private async Task<Registration> LoadAndCheckAsync(TestServiceScope scope, Registration registration,
            Action<Registration> check)
        {
            var updated = await scope.Db.Registrations
                .AsNoTracking()
                .SingleAsync(r =>
                    r.RegistrationId == registration.RegistrationId);

            check(updated);
            return updated;
        }

        public static object[][] GetRegInfoWithAdditionalInfoFilled()
        {
            return new[]
            {
                new object[]
                {
                    new Func<string, int, object>((userId, eventId) => new { userId, eventId, notes = "test" }),
                    new Action<Registration>(reg => Assert.Equal("test", reg.Notes))
                },
                new object[]
                {
                    new Func<string, int, object>((userId, eventId) => new { userId, eventId, type = 1 }),
                    new Action<Registration>(reg => Assert.Equal(Registration.RegistrationType.Student, reg.Type))
                },
                new object[]
                {
                    new Func<string, int, object>((userId, eventId) => new { userId, eventId, paymentMethod = 1 }),
                    new Action<Registration>(reg =>
                        Assert.Equal(PaymentMethod.PaymentProvider.EmailInvoice, reg.PaymentMethod))
                },
                new object[]
                {
                    new Func<string, int, object>((userId, eventId) =>
                        new { userId, eventId, customer = new { name = "test" } }),
                    new Action<Registration>(reg => Assert.Equal("test", reg.CustomerName))
                }
            };
        }
    }
}
