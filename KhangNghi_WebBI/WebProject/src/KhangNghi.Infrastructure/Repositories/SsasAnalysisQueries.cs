using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using KhangNghi.Core.DTOs;

namespace KhangNghi.Infrastructure.Repositories
{
    // Data access is exclusively through the existing SSAS MDX executor.
    public static class SsasAnalysisQueries
    {
        private sealed record Axis(string Hierarchy, string Level, string Label)
        {
            public string Members => Hierarchy + ".[" + Level + "].MEMBERS";
            public string Caption => Hierarchy + ".CURRENTMEMBER.MEMBER_CAPTION";
public string Key =>
    Hierarchy == "[Dim Time].[Quarter]" ||
    Hierarchy == "[Dim Time].[Month]" ||
    Hierarchy == "[Dim Location].[Region]"
        ? Caption
        : Hierarchy + ".CURRENTMEMBER.PROPERTIES('KEY0')";
        }
        private static readonly Dictionary<string, Axis> Axes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["year"] = new("[Dim Time].[Year]", "Year", "Năm"),
            ["quarter"] = new("[Dim Time].[Quarter]", "Quarter", "Quý"),
            ["month"] = new("[Dim Time].[Month]", "Month", "Tháng"),
            ["category"] = new("[Dim Product].[Category]", "Category", "Danh mục sản phẩm"),
            ["subcategory"] = new("[Dim Product].[Sub Category]", "Sub Category", "Nhóm sản phẩm"),
            ["product"] = new("[Dim Product].[Product Name]", "Product Name", "Sản phẩm"),
            ["channel"] = new("[Dim Sales Channel].[Channel Name]", "Channel Name", "Kênh bán hàng"),
            ["region"] = new("[Dim Location].[Region]", "Region", "Khu vực"),
            ["city"] = new("[Dim Location].[City]", "City", "Tỉnh/Thành phố"),
            ["segment"] = new("[Dim Customer].[Segment]", "Segment", "Phân khúc khách hàng"),
            ["customer"] = new("[Dim Customer].[Customer Name]", "Customer Name", "Khách hàng"),
            ["employee"] = new("[Dim Employee].[Employee Name]", "Employee Name", "Nhân viên"),
            ["manufacturer"] = new("[Dim Manufacturer].[Manufacturer Name]", "Manufacturer Name", "Hãng sản xuất"),
            ["payment"] = new("[Dim Payment Method].[Method Type]", "Method Type", "Thanh toán"),
            ["promotion"] = new("[Dim Promotion].[Promotion Name]", "Promotion Name", "Khuyến mãi"),
            ["service"] = new("[Dim Service].[Service Name]", "Service Name", "Dịch vụ"),
            ["supplier"] = new("[Dim Supplier].[Supplier Name]", "Supplier Name", "Nhà cung cấp"),
            ["warehouse"] = new("[Dim Warehouse].[Warehouse Name]", "Warehouse Name", "Kho"),
            ["shipper"] = new("[Dim Shipper].[Shipper Name]", "Shipper Name", "Đơn vị vận chuyển"),
            ["shippingmethod"] = new("[Dim Shipper].[Shipping Method]", "Shipping Method", "Phương thức giao")
        };
        private static readonly Dictionary<string, (string Measure, string Name, string Format)> Measures = new(StringComparer.OrdinalIgnoreCase)
        {
            ["revenue"] = ("Sales Amount", "Doanh thu", "vnd"),
            ["profit"] = ("Profit Amount", "Lợi nhuận", "vnd"),
            ["quantity"] = ("Quantity", "Số lượng bán", "number"),
            ["discount"] = ("Discount Amount", "Tiền giảm giá", "vnd"),
            ["shipping"] = ("Shipping Cost", "Chi phí vận chuyển", "vnd")
        };
        private const string Cube = "[KhangNghi_Cube]";
        private const int AnalysisStartYear = 2022;
        private const int AnalysisEndYear = 2025;
        private static Axis A(string key) => Axes.TryGetValue(key, out var a) ? a : throw new ArgumentException("Chiều phân tích không hợp lệ: " + key);
        private static string M(string name) => "[Measures].[" + name + "]";
        private static string Member(string axis, string key) => A(axis).Hierarchy + ".&[" + key.Replace("]", "]]") + "]";
        private static string Set(string axis, IEnumerable<string> keys)
{
    var values = keys.Distinct().ToArray();

    bool isQuarter = string.Equals(
        axis, "quarter", StringComparison.OrdinalIgnoreCase);

    bool isMonth = string.Equals(
        axis, "month", StringComparison.OrdinalIgnoreCase);

    if (isQuarter || isMonth)
    {
        int maxValue = isQuarter ? 4 : 12;
        var conditions = new List<string>();

        foreach (string value in values)
        {
            if (!int.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int number) ||
                number < 1 || number > maxValue)
            {
                throw new ArgumentException(
                    "Số quý/tháng không hợp lệ: " + value);
            }

            conditions.Add(
                "VAL(" + A(axis).Caption + ") = " +
                number.ToString(CultureInfo.InvariantCulture));
        }

        if (conditions.Count == 0)
            return "{}";

        return "FILTER(" + A(axis).Members + ", " +
            string.Join(" OR ", conditions) + ")";
    }
if (string.Equals(
    axis, "region", StringComparison.OrdinalIgnoreCase))
{
    if (values.Length == 0)
        return "{}";

    var comparisons = values.Select(value =>
        "(" + A(axis).Caption + " = \"" +
        value.Replace("\"", "\"\"") + "\")");

    return "FILTER(" + A(axis).Members + ", " +
        string.Join(" OR ", comparisons) + ")";
}
    return "{" +
        string.Join(",", values.Select(k => Member(axis, k))) +
        "}";
}
        private static string Subcube(string axis, IEnumerable<string> keys, string from) => "(SELECT " + Set(axis, keys) + " ON 0 FROM " + from + ")";
        private static string Cat(string axis) => A(axis).Caption;
        private static string Ratio(string numerator, string denominator) => "IIF(IsEmpty(" + denominator + ") OR " + denominator + " = 0, NULL, 100.0 * " + numerator + " / " + denominator + ")";
        private static string LatestYear =>
    "[Dim Time].[Year].&[" +
    AnalysisEndYear.ToString(CultureInfo.InvariantCulture) +
    "]";
        private static string LatestYearNumber => "VAL(" + LatestYear + ".PROPERTIES('KEY0'))";
        private static string SourceForLatestYear => "(SELECT {" + LatestYear + "} ON 0 FROM " + Cube + ")";
        private static string YM => "(VAL(" + A("year").Key + ") * 12 + VAL(" + A("month").Key + "))";

        public static double Number(Dictionary<string, object?> row, string key)
        {
            if (!row.TryGetValue(key, out var v)) throw new InvalidOperationException("SSAS thiếu cột: " + key);
            return v == null || v == DBNull.Value ? 0 : Convert.ToDouble(v, CultureInfo.InvariantCulture);
        }
        private static string Text(Dictionary<string, object?> row, string key) => row.TryGetValue(key, out var v) && v != DBNull.Value ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? "" : "";
        private static string Query(string axes, string label, string value, string? value2 = null, string? value3 = null,
            string? group = null, string? note = null, string? source = null, string? nonempty = null, bool descending = true, int top = 0, string extra = "", string? order = null)
        {
            string defs = "MEMBER [Measures].[Label] AS " + label + "\nMEMBER [Measures].[Value] AS " + value;
            if (value2 != null) defs += "\nMEMBER [Measures].[Value2] AS " + value2;
            if (value3 != null) defs += "\nMEMBER [Measures].[Value3] AS " + value3;
            if (group != null) defs += "\nMEMBER [Measures].[Grp] AS " + group;
            if (note != null) defs += "\nMEMBER [Measures].[Note] AS " + note;
            var cols = new List<string> { M("Label"), M("Value") };
            if (value2 != null) cols.Add(M("Value2"));
            if (value3 != null) cols.Add(M("Value3"));
            if (group != null) cols.Add(M("Grp"));
            if (note != null) cols.Add(M("Note"));
            string rows = "ORDER(NONEMPTY(" + axes + ",{" + (nonempty ?? M("Value")) + "})," + (order ?? M("Value")) + "," + (descending ? "BDESC" : "BASC") + ")";
            if (top > 0) rows = "HEAD(" + rows + "," + top + ")";
            return "WITH\n" + extra + "\n" + defs + "\nSELECT {" + string.Join(",", cols) + "} ON 0,\n" + rows + " ON 1\nFROM " + (source ?? Cube) + ";";
        }

        public static string GetQuestionMdx(int n)
        {
            string rev=M("Sales Amount"), profit=M("Profit Amount"), qty=M("Quantity"), cost=M("Total Cost"), buy=M("Purchase Quantity"), stock=M("Quantity On Hand");
            string ax(string key) => A(key).Members;
            string cross(string a,string b) => "("+ax(a)+" * "+ax(b)+")";
            string south = Subcube("region",new[]{"Miền Nam"},Cube);
            string latestSouth = Subcube("region",new[]{"Miền Nam"},SourceForLatestYear);
            switch(n)
            {
                case 1: return Query(ax("year"),Cat("year"),rev,profit,qty,descending:false,order:"VAL("+A("year").Key+")");
                case 2: return Query(ax("quarter"),"'Quý ' + "+Cat("quarter"),rev,note:LatestYearNumber,source:SourceForLatestYear,descending:false,order:"VAL("+A("quarter").Key+")");
                case 3: return Query(ax("month"),"'T' + "+Cat("month"),rev,note:LatestYearNumber,source:SourceForLatestYear,descending:false,order:"VAL("+A("month").Key+")");
                case 4: return Query(ax("category"),Cat("category"),rev,profit);
                case 5: return Query(cross("category","subcategory"),Cat("subcategory"),rev,group:Cat("category"));
                case 6: return Query(ax("product"),Cat("product"),rev,top:10);
                case 7: return Query(ax("product"),Cat("product"),profit,top:10);
                case 8: return Query(ax("product"),Cat("product"),rev,descending:false,top:5);
                case 9: return Query(ax("channel"),Cat("channel"),rev,M("Discount Amount"));
                case 10: return Query(ax("payment"),Cat("payment"),rev,M("Order Count"));
                case 11: return Query(ax("segment"),Cat("segment"),rev);
                case 12: return Query(ax("customer"),Cat("customer"),rev,top:10);
                case 13: return Query(ax("region"),Cat("region"),rev,profit);
                case 14: return Query(cross("region","city"),Cat("city"),rev,group:Cat("region"));
                case 15: return Query(ax("category"),Cat("category"),rev,note:LatestYearNumber,source:SourceForLatestYear);
                case 16: return Query(ax("category"),Cat("category"),rev,note:LatestYearNumber,source:latestSouth);
                case 17: return Query(cross("category","region"),Cat("category"),rev,group:Cat("region"));
                case 18: return Query(ax("city"),Cat("city"),profit,top:5);
                case 19: return Query(ax("employee"),Cat("employee"),rev,top:10);
                case 20: return Query(ax("promotion"),Cat("promotion"),rev,M("Discount Amount"),profit,order:profit);
                case 21: return Query(ax("manufacturer"),Cat("manufacturer"),rev);
                case 22: return Query(ax("category"),Cat("category"),Ratio(profit,rev),nonempty:rev);
                case 23: return Query(ax("service"),Cat("service"),rev);
                case 24: return Query(cross("channel","quarter"),Cat("channel"),rev,group:"'Quý ' + "+Cat("quarter"),note:LatestYearNumber,source:SourceForLatestYear);
                case 25: return Query(ax("year"),Cat("year"),cost,buy,descending:false,order:"VAL("+A("year").Key+")");
                case 26: return Query(ax("supplier"),Cat("supplier"),cost,top:5);
                case 27: return Query(ax("category"),Cat("category"),cost);
                case 28: return Query(ax("supplier"),Cat("supplier"),"IIF("+buy+"=0,NULL,"+cost+"/"+buy+")",nonempty:buy,descending:false);
                case 29: return Query(ax("warehouse"),Cat("warehouse"),"COUNT(NONEMPTY([Dim Purchase Order].[Purchase Order ID].[Purchase Order ID].MEMBERS,{"+cost+"}))",buy,nonempty:cost);
                case 30: return Query(ax("warehouse"),Cat("warehouse"),stock,M("Quantity In"),M("Quantity Out"));
                case 31: return Query(ax("category"),Cat("category"),stock);
                                case 32:
                case 33:
                {
                    string months = cross("year", "month");

                    string extra =
                        "SET [Web Calendar Months] AS " + months +

                        "\nSET [Web Latest Sales Month] AS " +
                        "TAIL(ORDER(" +
                            "NONEMPTY([Web Calendar Months],{" + qty + "})," +
                            YM + ",BASC),1)" +

                        "\nSET [Web Recent Sales Months] AS " +
                        "FILTER([Web Calendar Months]," +
                            YM + " > MAX([Web Latest Sales Month]," +
                                YM + ") - 3 AND " +
                            YM + " <= MAX([Web Latest Sales Month]," +
                                YM + "))" +

                        "\nMEMBER [Measures].[Web Sold 90] AS " +
                        "SUM([Web Recent Sales Months]," + qty + ")" +

                        "\nMEMBER [Measures].[Web Cover] AS " +
                        "IIF(" +
                            "ISEMPTY([Measures].[Web Sold 90]) OR " +
                            "[Measures].[Web Sold 90] <= 0," +
                            "NULL," +
                            "90.0 * " + stock +
                            " / [Measures].[Web Sold 90])" +

                        "\nMEMBER [Measures].[Web Deficit] AS " +
                        "IIF(" +
                            "[Measures].[Web Sold 90] / 30.0 > " + stock + "," +
                            "-INT(-([Measures].[Web Sold 90] / 30.0 - " +
                                stock + "))," +
                            "0)";

                    return Query(
                        ax("product"),
                        Cat("product"),
                        n == 32 ? M("Web Cover") : M("Web Deficit"),
                        stock,
                        n == 32 ? M("Web Deficit") : M("Web Cover"),
                        nonempty: stock + "," + M("Web Sold 90"),
                        descending: n == 33,
                        extra: extra);
                }
                case 34: return Query(ax("product"),Cat("product"),stock,top:10);
                case 35:
                    string last12="TAIL(ORDER(NONEMPTY("+cross("year","month")+",{"+M("Quantity Out")+"}),"+YM+",BASC),12)";
                    return Query(last12,Cat("month")+" + '/' + "+Cat("year"),M("Quantity Out"),descending:false,order:YM);
                case 36: return Query(ax("shipper"),Cat("shipper"),M("Shipping Cost"),M("Fact Shipping Count"));
                case 37: return Query(ax("shippingmethod"),Cat("shippingmethod"),"IIF([Measures].[Known Deliveries]=0,NULL,[Measures].[Delivery Days]/[Measures].[Known Deliveries])",Ratio(M("On Time Deliveries"),M("Known Deliveries")),M("Known Deliveries"),nonempty:M("Known Deliveries"),descending:false);
                case 38: return Query(ax("shipper"),Cat("shipper"),M("Late Days"),M("Late Deliveries"));
                case 39: return Query(ax("region"),Cat("region"),M("Shipping Cost"));
                case 40: return Query(ax("region"),Cat("region"),Ratio(M("Shipping Cost"),rev),M("Shipping Cost"),rev,note:Ratio("("+M("Shipping Cost")+",[Dim Location].[Region].DEFAULTMEMBER)","("+rev+",[Dim Location].[Region].DEFAULTMEMBER)"),nonempty:rev);
                case 41: return Query(ax("region"),Cat("region"),rev,profit);
                case 42: return Query(ax("category"),Cat("category"),rev,profit,Ratio(profit,rev));
                case 43: return Query(ax("month"),"'Tháng ' + "+Cat("month"),rev,note:LatestYearNumber,source:Subcube("quarter",new[]{"4"},SourceForLatestYear),descending:false,order:"VAL("+A("month").Key+")");
                case 44: return Query(ax("category"),Cat("category"),rev,source:south);
                case 45: return Query(ax("year"),Cat("year"),rev,profit,source:Subcube("channel",new[]{"Website Khang Nghị"},Cube),descending:false,order:"VAL("+A("year").Key+")");
                case 46: return Query(ax("category"),Cat("category"),rev,source:Subcube("year",new[]{"2024","2025"},Subcube("region",new[]{"Miền Bắc","Miền Trung"},Subcube("channel",new[]{"Đại lý","Cửa hàng"},Cube))));
                case 47: return Query(ax("supplier"),Cat("supplier"),cost,buy,source:Subcube("year",new[]{"2025"},Subcube("warehouse",new[]{"Kho TP. Hồ Chí Minh"},Cube)));
                case 48: return Query(cross("year","region"),Cat("year"),rev,group:Cat("region"),descending:false,order:"VAL("+A("year").Key+")");
                                case 49:
                {
                    string previousYear =
                        (AnalysisEndYear - 1).ToString(
                            CultureInfo.InvariantCulture);

                    string previousYearMember =
                        Member("year", previousYear);

                    string previousQuarterMember =
                        "STRTOMEMBER(" +
                        "'[Dim Time].[Quarter].&[" +
                        previousYear + "]&[' + " +
                        Cat("quarter") +
                        " + ']', CONSTRAINED)";

                    string previousRevenue =
                        "(" + rev + "," +
                        previousYearMember + "," +
                        previousQuarterMember + ")";

                    string extra =
                        "MEMBER [Measures].[Web Previous Quarter Revenue] AS " +
                        previousRevenue;

                    string previous =
                        M("Web Previous Quarter Revenue");

                    string growth =
                        "IIF(ISEMPTY(" + previous + ") OR " +
                        previous + " = 0, NULL, " +
                        "100.0 * (" + rev + " - " + previous + ") / " +
                        previous + ")";

                    string query = Query(
                        ax("quarter"),
                        "'Quý ' + " + Cat("quarter"),
                        growth,
                        rev,
                        previous,
                        note: AnalysisEndYear.ToString(
                            CultureInfo.InvariantCulture),
                        nonempty: rev,
                        descending: false,
                        order: "VAL(" + A("quarter").Key + ")",
                        extra: extra);

                    return query.TrimEnd(';') +
                        " WHERE (" + LatestYear + ");";
                }
                case 50: return Query(cross("category","warehouse"),Cat("category"),stock,group:Cat("warehouse"));
                default: throw new ArgumentOutOfRangeException(nameof(n));
            }
        }

        public static Task<List<Dictionary<string, object?>>> GetQuestionAnswerFromCubeAsync(this ISsasRepository repository,int n) => repository.ExecuteMdxQueryAsync(GetQuestionMdx(n));

        public static async Task<object?> GetAnalysisQuickChartFromCubeAsync(this ISsasRepository repository,string id)
        {
            if(!int.TryParse(id.Replace("MDX-","",StringComparison.OrdinalIgnoreCase),out int n)||n<19||n>24)return null;
            string key=n switch {19=>"employee",20=>"promotion",21=>"manufacturer",22=>"category",23=>"service",_=>"channel"};
            string axes=A(key).Members;
            if(n==24)axes="("+axes+" * "+A("quarter").Members+")";
            string mdx="WITH MEMBER [Measures].[Label] AS "+Cat(key)+"\nMEMBER [Measures].[Revenue] AS [Measures].[Sales Amount]\nMEMBER [Measures].[Profit] AS [Measures].[Profit Amount]\nMEMBER [Measures].[OrderCount] AS [Measures].[Order Count]\nMEMBER [Measures].[ProfitMarginPct] AS "+Ratio(M("Profit Amount"),M("Sales Amount"))+"\nMEMBER [Measures].[Channel] AS "+Cat("channel")+"\nMEMBER [Measures].[QuarterValue] AS VAL("+A("quarter").Key+")\nSELECT {[Measures].[Label],[Measures].[Revenue],[Measures].[Profit],[Measures].[OrderCount],[Measures].[ProfitMarginPct],[Measures].[Discount Amount],[Measures].[Quantity]"+(n==24?",[Measures].[Channel],[Measures].[QuarterValue]":"")+"} ON 0, NONEMPTY("+axes+",{[Measures].[Sales Amount]}) ON 1 FROM "+(n==24?SourceForLatestYear:Cube)+";";
            var rows=await repository.ExecuteMdxQueryAsync(mdx);
            foreach(var row in rows)
            {
                row["DiscountAmount"]=row["Discount Amount"];
                if(n==24)row["Quarter"]=row["QuarterValue"];
            }
            var meta=repository.Get50MdxQueriesFromCube().First(q=>q.OrderNumber==n);
            return new {id=meta.Id,title=meta.Title,purpose=meta.BusinessPurpose,section=meta.Section,chartType=n==24?"matrix":"bar",dimension=A(key).Label,measures=new[]{"Doanh thu","Lợi nhuận","Số đơn"},relatedRoute="sales",data=rows};
        }

        public static async Task<object> GetMultiDimFromCubeAsync(this ISsasRepository repository,string measure,string row,string col)
        {
            if (!Measures.TryGetValue(measure, out var m)) throw new ArgumentException("Số liệu không hợp lệ.");
            var r=A(row); bool hasCol=!string.IsNullOrWhiteSpace(col)&&col!="none"&&!string.Equals(row,col,StringComparison.OrdinalIgnoreCase);
            Axis? c=hasCol?A(col):null;
            var shippingAxes=new[]{"year","quarter","month","region","city"};
            if(measure=="shipping"&&(!shippingAxes.Contains(row)||(hasCol&&!shippingAxes.Contains(col))))throw new ArgumentException("Chi phí vận chuyển chỉ hỗ trợ chiều thời gian và địa lý.");
string rowAxis = c == null
    ? r.Members
    : "(" + r.Members + " * " + c.Members + ")";

string colDefinition = c == null
    ? ""
    : "\nMEMBER [Measures].[ColKey] AS " + c.Caption;

string selectedColumns = c == null
    ? "[Measures].[RowKey]," + M(m.Measure)
    : "[Measures].[RowKey],[Measures].[ColKey]," + M(m.Measure);

string mdx =
    "WITH\nMEMBER [Measures].[RowKey] AS " + r.Caption +
    colDefinition +
    "\nSELECT {" + selectedColumns + "} ON 0," +
    "\nNONEMPTY(" + rowAxis + ",{" + M(m.Measure) + "}) ON 1" +
    "\nFROM " + Cube + ";";

var raw = await repository.ExecuteMdxQueryAsync(mdx);

var cells = raw
    .GroupBy(x => (
        R: Text(x, "RowKey"),
        C: c == null ? "Tổng" : Text(x, "ColKey")))
    .ToDictionary(
        g => g.Key,
        g => g.Sum(x => Number(x, m.Measure)));
            var rs=cells.Keys.Select(x=>x.R).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToList();
            var cs=cells.Keys.Select(x=>x.C).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToList();
            double Cell(string x,string y)=>cells.TryGetValue((x,y),out var v)?v:0;
            double Total(string x)=>cs.Sum(y=>Cell(x,y));
            var best=rs.OrderByDescending(Total).FirstOrDefault();
            double grand=rs.Sum(Total);
            if(c==null)
            {
                var items=rs.OrderByDescending(Total).Select(x=>new {label=x,value=Total(x),percent=grand==0?0:Total(x)*100/grand}).ToList();
                return new {isMultiDim=false,measure=m.Name,measureFormat=m.Format,rowLabel=r.Label,colLabel="Không chia cột",summary=new {total=grand,topRow=best??"—",topRowValue=best==null?0:Total(best),rowCount=rs.Count,colCount=1},chart=new {type="bar",items},table=new {columns=new object[]{new {key="label",label=r.Label},new {key="value",label=m.Name,format=m.Format},new {key="percent",label="Tỷ trọng (%)",format="percent"}},rows=items}};
            }
            var tableRows=rs.Select(x=>{var d=new Dictionary<string,object?>{{"RowName",x},{"Total",Total(x)}};foreach(var y in cs)d[y]=Cell(x,y);return d;}).ToList();
            var columns=new List<object>{new {key="RowName",label=r.Label}};
            columns.AddRange(cs.Select(x=>(object)new {key=x,label=x,format=m.Format}));columns.Add(new {key="Total",label="Tổng",format=m.Format});
            return new {isMultiDim=true,measure=m.Name,measureFormat=m.Format,rowLabel=r.Label,colLabel=c.Label,summary=new {total=grand,topRow=best??"—",topRowValue=best==null?0:Total(best),rowCount=rs.Count,colCount=cs.Count},chart=new {type="groupedBar",categories=rs,series=cs.Select(y=>new {name=y,data=rs.Select(x=>Cell(x,y)).ToList()}).ToList()},table=new {columns,rows=tableRows}};
        }

        public static async Task<object> GetOlapOptionsFromCubeAsync(this ISsasRepository repository)
        {
            async Task<List<string>> Values(string key)
            {
                var rows=await repository.ExecuteMdxQueryAsync(Query(A(key).Members,Cat(key),M("Sales Amount")));
                return rows.Select(x=>Text(x,"Label")).Where(x=>x.Length>0).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToList();
            }
            var yearStrings=await Values("year");
            var years=yearStrings.Where(x=>int.TryParse(x,out _)).Select(x=>int.Parse(x,CultureInfo.InvariantCulture)).OrderBy(x=>x).ToList();
            var regions=await Values("region");var channels=await Values("channel");var categories=await Values("category");
            return new {years,regions,channels,categories};
        }

        public static async Task<object> GetOlapFromCubeAsync(this ISsasRepository repository,string hierarchy,string[] path,string measure,int[] years,string[] regions,string[] channels,string[] categories)
        {
            string[] levels=hierarchy switch {"time"=>new[]{"year","quarter","month"},"geo"=>new[]{"region","city"},"product"=>new[]{"category","subcategory","product"},_=>throw new ArgumentException("Phân cấp không hợp lệ.")};
            if(path.Length>=levels.Length)throw new ArgumentException("Đường khoan sâu vượt cấp lá.");
            if(measure=="shipping"||!Measures.TryGetValue(measure,out var m))throw new ArgumentException("Số liệu OLAP không hợp lệ.");
            if(years.Any(y=>y<1900||y>2100))throw new ArgumentException("Năm không hợp lệ.");
            string[] Clean(string[] values)=>values.Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Trim()).Distinct().ToArray();
            regions=Clean(regions);channels=Clean(channels);categories=Clean(categories);years=years.Distinct().OrderBy(x=>x).ToArray();
            if(new[]{path.Length,years.Length,regions.Length,channels.Length,categories.Length}.Any(x=>x>100))throw new ArgumentException("Quá nhiều giá trị lọc.");
            string source=Cube;
            if(years.Length>0)source=Subcube("year",years.Select(x=>x.ToString(CultureInfo.InvariantCulture)),source);
            if(regions.Length>0)source=Subcube("region",regions,source);
            if(channels.Length>0)source=Subcube("channel",channels,source);
            if(categories.Length>0)source=Subcube("category",categories,source);
            for(int i=0;i<path.Length;i++)source=Subcube(levels[i],new[]{path[i]},source);
            string key=levels[path.Length];var a=A(key);
            bool numeric=hierarchy=="time";
            string mdx=Query(a.Members,a.Key,M(m.Measure),source:source,descending:!numeric,order:numeric?"VAL("+a.Key+")":null);
            var raw=await repository.ExecuteMdxQueryAsync(mdx);double total=raw.Sum(x=>Number(x,"Value"));
            string Label(string k,string v)=>k=="quarter"?"Quý "+v:k=="month"?"Tháng "+v:v;
            var rows=raw.Select(x=>{string v=Text(x,"Label");double value=Number(x,"Value");return new {key=v,label=Label(key,v),value,share=total==0?0:value*100/total};}).ToList();
            int filterDims=new[]{years.Length,regions.Length,channels.Length,categories.Length}.Count(x=>x>0);
            int filterValues=years.Length+regions.Length+channels.Length+categories.Length;
            return new {hierarchy,hierarchyName=hierarchy=="time"?"Thời gian":hierarchy=="geo"?"Địa lý":"Sản phẩm",measure=m.Name,measureFormat=m.Format,levels=levels.Select(x=>A(x).Label).ToList(),depth=path.Length,levelLabel=a.Label,canDrill=path.Length<levels.Length-1,path=path.Select((v,i)=>new {level=A(levels[i]).Label,key=v,label=Label(levels[i],v)}).ToList(),filters=new {years,regions,channels,categories},filterOperation=filterDims==0?"none":filterDims==1&&filterValues==1?"slice":"dice",total,rows,mdx};
        }

        public static List<MdxCatalogItemDto> Get50MdxQueriesFromCube(this ISsasRepository repository)
        {
            return new List<MdxCatalogItemDto>
            {
                new() { Id = "MDX-01", Section = "Bán hàng & Doanh thu", Title = "Tổng Doanh thu, Lợi nhuận và Số lượng bán theo từng Năm", BusinessPurpose = "Báo cáo tăng trưởng dài hạn cho Ban Giám đốc. Dữ liệu cấp dòng đơn hàng theo ngày được cộng gộp lên cấp năm.", OperationType = "Roll-up", SourceFactTable = "Fact_Sales", OrderNumber = 1, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(1) },
                new() { Id = "MDX-02", Section = "Bán hàng & Doanh thu", Title = "Doanh thu năm 2025 khoan sâu xuống Quý và Tháng", BusinessPurpose = "Ban Giám đốc theo dõi tiến độ doanh thu năm gần nhất có đủ dữ liệu, từ tổng năm xuống từng quý rồi từng tháng.", OperationType = "Drill-down", SourceFactTable = "Fact_Sales", OrderNumber = 2, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(2) },
                new() { Id = "MDX-03", Section = "Bán hàng & Doanh thu", Title = "Doanh số bán hàng chi tiết theo từng Tháng trong năm (Xu hướng chu kỳ mùa vụ)", BusinessPurpose = "Đánh giá biến động doanh số qua các tháng (gộp mọi năm) để lập kế hoạch nhập hàng theo mùa vụ.", OperationType = "Time Trend", SourceFactTable = "Fact_Sales", OrderNumber = 3, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(3) },
                new() { Id = "MDX-04", Section = "Bán hàng & Doanh thu", Title = "Doanh thu và Lợi nhuận theo từng Danh mục sản phẩm (Category)", BusinessPurpose = "Xác định nhóm hàng mũi nhọn trong 7 danh mục thiết bị điện – tự động hóa (Biến tần, Thiết bị tự động hóa, Thiết bị điều khiển, …).", OperationType = "Category Aggregation", SourceFactTable = "Fact_Sales", OrderNumber = 4, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(4) },
                new() { Id = "MDX-05", Section = "Bán hàng & Doanh thu", Title = "Khoan sâu từ Danh mục \"Biến tần\" xuống Phân nhóm sản phẩm", BusinessPurpose = "Danh mục Biến tần chiếm hơn một nửa doanh thu. Trưởng phòng Kinh doanh cần biết phân nhóm nào tạo ra doanh thu đó.", OperationType = "Drill-down", SourceFactTable = "Fact_Sales", OrderNumber = 5, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(5) },
                new() { Id = "MDX-06", Section = "Bán hàng & Doanh thu", Title = "Top 10 Sản phẩm mang lại Doanh thu cao nhất (Top-N Analysis)", BusinessPurpose = "Lọc ra 10 mặt hàng đóng góp doanh thu lớn nhất toàn công ty.", OperationType = "Top-N", SourceFactTable = "Fact_Sales", OrderNumber = 6, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(6) },
                new() { Id = "MDX-07", Section = "Bán hàng & Doanh thu", Title = "Top 10 Sản phẩm có Lợi nhuận gộp cao nhất", BusinessPurpose = "Phát hiện 10 sản phẩm sinh lời tốt nhất, độc lập với doanh số.", OperationType = "Top-N", SourceFactTable = "Fact_Sales", OrderNumber = 7, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(7) },
                new() { Id = "MDX-08", Section = "Bán hàng & Doanh thu", Title = "Top 5 Sản phẩm bán chậm nhất (Bottom-N Analysis)", BusinessPurpose = "Nhận diện mặt hàng bán chậm để có giải pháp xả hàng hoặc ngừng nhập.", OperationType = "Bottom-N", SourceFactTable = "Fact_Sales", OrderNumber = 8, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(8) },
                new() { Id = "MDX-09", Section = "Bán hàng & Doanh thu", Title = "Cơ cấu Doanh thu và Chiết khấu theo Kênh bán hàng (Sales Channel)", BusinessPurpose = "So sánh hiệu quả 4 kênh: Đại lý, Telesale trực tiếp, Website Khang Nghị và Cửa hàng.", OperationType = "Dimension Slicing", SourceFactTable = "Fact_Sales", OrderNumber = 9, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(9) },
                new() { Id = "MDX-10", Section = "Bán hàng & Doanh thu", Title = "Phân bổ Doanh thu theo Hình thức thanh toán (Payment Method)", BusinessPurpose = "Đánh giá cơ cấu thanh toán của khách hàng để quản lý công nợ.", OperationType = "Dimension Slicing", SourceFactTable = "Fact_Sales", OrderNumber = 10, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(10) },
                new() { Id = "MDX-11", Section = "Khách hàng & Thị trường", Title = "Doanh thu theo Phân khúc khách hàng (Segment)", BusinessPurpose = "Đo tỷ trọng doanh số của 6 phân khúc: Hộ kinh doanh nhỏ lẻ, Xưởng sản xuất, Nhà thầu xây dựng, Thợ điện dân dụng, Nhà máy và Chủ nhà.", OperationType = "Customer Segmentation", SourceFactTable = "Fact_Sales", OrderNumber = 11, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(11) },
                new() { Id = "MDX-12", Section = "Khách hàng & Thị trường", Title = "Top 10 Khách hàng có giá trị mua hàng lớn nhất", BusinessPurpose = "Chăm sóc khách hàng trọng điểm và xây dựng chính sách chiết khấu riêng.", OperationType = "Top-N", SourceFactTable = "Fact_Sales", OrderNumber = 12, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(12) },
                new() { Id = "MDX-13", Section = "Khách hàng & Thị trường", Title = "Doanh thu và Lợi nhuận theo Khu vực địa lý (Miền Bắc, Miền Trung, Miền Nam)", BusinessPurpose = "Đánh giá hiệu quả kinh doanh theo vùng miền.", OperationType = "Geographic Aggregation", SourceFactTable = "Fact_Sales", OrderNumber = 13, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(13) },
                new() { Id = "MDX-14", Section = "Khách hàng & Thị trường", Title = "Khoan sâu Doanh số từ \"Miền Nam\" xuống Tỉnh/Thành phố", BusinessPurpose = "Miền Nam chiếm khoảng 66% doanh thu. Cần biết tỉnh/thành nào trong vùng tiêu thụ mạnh nhất.", OperationType = "Drill-down", SourceFactTable = "Fact_Sales", OrderNumber = 14, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(14) },
                new() { Id = "MDX-15", Section = "Khách hàng & Thị trường", Title = "Doanh thu theo nhóm sản phẩm trong năm gần nhất", BusinessPurpose = "Cố định một lát cắt trên chiều Thời gian (năm 2025) bằng mệnh đề WHERE để xem cơ cấu doanh thu sản phẩm trong năm.", OperationType = "Slice", SourceFactTable = "Fact_Sales", OrderNumber = 15, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(15) },
                new() { Id = "MDX-16", Section = "Khách hàng & Thị trường", Title = "Doanh thu sản phẩm đồng thời theo Năm 2025 và Khu vực \"Miền Nam\"", BusinessPurpose = "Thu hẹp khối dữ liệu theo đồng thời hai chiều (Thời gian và Địa lý) để đánh giá thị trường trọng điểm trong năm gần nhất.", OperationType = "Dice", SourceFactTable = "Fact_Sales", OrderNumber = 16, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(16) },
                new() { Id = "MDX-17", Section = "Khách hàng & Thị trường", Title = "Thao tác PIVOT (Xoay chiều) – Ma trận chéo giữa Danh mục sản phẩm và Vùng miền", BusinessPurpose = "Đưa Vùng miền lên cột và Danh mục xuống dòng để so sánh tỷ trọng chéo.", OperationType = "Pivot", SourceFactTable = "Fact_Sales", OrderNumber = 17, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(17) },
                new() { Id = "MDX-18", Section = "Khách hàng & Thị trường", Title = "Top 5 Thành phố đóng góp Lợi nhuận cao nhất cả nước", BusinessPurpose = "Xác định các đô thị sinh lời cao nhất để ưu tiên nguồn lực bán hàng.", OperationType = "Top-N", SourceFactTable = "Fact_Sales", OrderNumber = 18, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(18) },
                new() { Id = "MDX-19", Section = "Nhân sự & Khuyến mãi", Title = "Đánh giá KPI Doanh số bán hàng theo từng Nhân viên (Dim Employee)", BusinessPurpose = "Đánh giá năng suất đội ngũ bán hàng làm căn cứ tính hoa hồng.", OperationType = "Ranking Order", SourceFactTable = "Fact_Sales", OrderNumber = 19, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(19) },
                new() { Id = "MDX-20", Section = "Nhân sự & Khuyến mãi", Title = "Đánh giá hiệu quả các chương trình Khuyến mãi (Promotion Name)", BusinessPurpose = "So sánh tiền chiết khấu đã chi với doanh thu mang lại của từng chương trình.", OperationType = "Promotion Analysis", SourceFactTable = "Fact_Sales", OrderNumber = 20, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(20) },
                new() { Id = "MDX-21", Section = "Nhân sự & Khuyến mãi", Title = "Doanh số sản phẩm bán ra theo Hãng sản xuất (Dim Manufacturer)", BusinessPurpose = "Xác định thương hiệu được thị trường ưa chuộng để ưu tiên nhập hàng.", OperationType = "Manufacturer Breakdown", SourceFactTable = "Fact_Sales", OrderNumber = 21, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(21) },
                new() { Id = "MDX-22", Section = "Nhân sự & Khuyến mãi", Title = "Calculated Member – Tỷ suất lợi nhuận (Profit Margin %) theo Danh mục", BusinessPurpose = "Dùng WITH MEMBER tính Lợi nhuận / Doanh thu tức thời mà không sửa CSDL.", OperationType = "Calculated Member", SourceFactTable = "Fact_Sales", OrderNumber = 22, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(22) },
                new() { Id = "MDX-23", Section = "Nhân sự & Khuyến mãi", Title = "Phân tích Doanh thu theo Gói Dịch vụ đính kèm (Dim Service)", BusinessPurpose = "Đo lường doanh thu từ các gói bảo hành, dịch vụ vận chuyển và lắp đặt hoàn thiện.", OperationType = "Service Breakdown", SourceFactTable = "Fact_Sales", OrderNumber = 23, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(23) },
                new() { Id = "MDX-24", Section = "Nhân sự & Khuyến mãi", Title = "Ma trận CROSSJOIN giữa Kênh bán hàng và Quý của năm 2025", BusinessPurpose = "Đối chiếu diễn biến doanh thu của từng kênh qua các quý năm 2025.", OperationType = "Crossjoin", SourceFactTable = "Fact_Sales", OrderNumber = 24, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(24) },
                new() { Id = "MDX-25", Section = "Mua hàng & Nhà cung cấp", Title = "Tổng Chi phí mua hàng và Số lượng nhập theo từng Năm", BusinessPurpose = "Kiểm soát dòng tiền chi mua hàng qua các năm tài chính.", OperationType = "Roll-up", SourceFactTable = "Fact_Purchase", OrderNumber = 25, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(25) },
                new() { Id = "MDX-26", Section = "Mua hàng & Nhà cung cấp", Title = "Top 5 Nhà cung cấp có giá trị mua vào lớn nhất", BusinessPurpose = "Đánh giá mức độ phụ thuộc vào các nhà cung ứng trọng yếu.", OperationType = "Top-N", SourceFactTable = "Fact_Purchase", OrderNumber = 26, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(26) },
                new() { Id = "MDX-27", Section = "Mua hàng & Nhà cung cấp", Title = "Chi phí nhập hàng phân bổ theo từng Danh mục sản phẩm", BusinessPurpose = "Đối chiếu tỷ trọng vốn nhập hàng giữa các nhóm sản phẩm.", OperationType = "Category Aggregation", SourceFactTable = "Fact_Purchase", OrderNumber = 27, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(27) },
                new() { Id = "MDX-28", Section = "Mua hàng & Nhà cung cấp", Title = "Đơn giá bình quân nhập hàng theo Nhà cung cấp (Calculated Measure)", BusinessPurpose = "So sánh giá nhập bình quân giữa các nhà cung ứng để đàm phán giá.", OperationType = "Calculated Member", SourceFactTable = "Fact_Purchase", OrderNumber = 28, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(28) },
                new() { Id = "MDX-29", Section = "Mua hàng & Nhà cung cấp", Title = "Số đơn mua phân biệt theo kho", BusinessPurpose = "Đánh giá tần suất nhập hàng tại từng kho.", OperationType = "Warehouse Aggregation", SourceFactTable = "Fact_Purchase", OrderNumber = 29, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(29) },
                new() { Id = "MDX-30", Section = "Quản lý Kho & Tồn kho", Title = "Tổng Nhập, Xuất và Tồn kho cuối kỳ theo từng Kho hàng", BusinessPurpose = "Thủ kho kiểm soát dòng luân chuyển và tồn hiện hành của từng kho.", OperationType = "Inventory Status", SourceFactTable = "Fact_Inventory", OrderNumber = 30, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(30) },
                new() { Id = "MDX-31", Section = "Quản lý Kho & Tồn kho", Title = "Tồn kho cuối kỳ phân bổ theo Danh mục sản phẩm", BusinessPurpose = "Tránh tồn đọng vốn quá mức ở một danh mục.", OperationType = "Category Aggregation", SourceFactTable = "Fact_Inventory", OrderNumber = 31, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(31) },
                new() { Id = "MDX-32", Section = "Quản lý Kho & Tồn kho", Title = "Cảnh báo Tồn kho chạm ngưỡng tái đặt hàng (FILTER + WITH MEMBER)", BusinessPurpose = "Lọc các sản phẩm có tồn kỳ cuối **nhỏ hơn hoặc bằng** ngưỡng tái đặt hàng để Trưởng phòng Mua hàng lên kế hoạch nhập.", OperationType = "Filter Condition", SourceFactTable = "Fact_Inventory", OrderNumber = 32, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(32) },
                new() { Id = "MDX-33", Section = "Quản lý Kho & Tồn kho", Title = "Mức thiếu hụt so với tồn kho an toàn (Stock Deficit) cho từng mặt hàng", BusinessPurpose = "Tính số lượng cần bổ sung để đạt **gấp đôi** ngưỡng tái đặt hàng (mức an toàn đề xuất). Hệ số 2 là tham số nghiệp vụ, có thể điều chỉnh.", OperationType = "Calculated Member", SourceFactTable = "Fact_Inventory", OrderNumber = 33, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(33) },
                new() { Id = "MDX-34", Section = "Quản lý Kho & Tồn kho", Title = "Top 10 Sản phẩm có Tồn kho cuối kỳ cao nhất (Nguy cơ ứ đọng vốn)", BusinessPurpose = "Giúp quản lý kho lên phương án giải phóng hàng tồn.", OperationType = "Top-N", SourceFactTable = "Fact_Inventory", OrderNumber = 34, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(34) },
                new() { Id = "MDX-35", Section = "Quản lý Kho & Tồn kho", Title = "Biến động lượng hàng Xuất kho theo từng Tháng của năm 2025", BusinessPurpose = "Phân tích nhịp độ xuất kho để bố trí nhân lực đóng gói và giao hàng. Quantity Out là độ đo cộng dồn được, nên được phép Sum theo thời gian.", OperationType = "Time Trend", SourceFactTable = "Fact_Inventory", OrderNumber = 35, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(35) },
                new() { Id = "MDX-36", Section = "Vận chuyển & Logistics", Title = "Tổng Chi phí vận chuyển theo từng Đơn vị/Phương thức vận chuyển", BusinessPurpose = "Theo dõi ngân sách cước vận chuyển.", OperationType = "Shipper Aggregation", SourceFactTable = "Fact_Shipping", OrderNumber = 36, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(36) },
                new() { Id = "MDX-37", Section = "Vận chuyển & Logistics", Title = "Hiệu suất giao hàng theo Phương thức vận chuyển", BusinessPurpose = "So sánh cước phí và **số ngày giao trung bình** giữa các phương thức.", OperationType = "Method Breakdown", SourceFactTable = "Fact_Shipping", OrderNumber = 37, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(37) },
                new() { Id = "MDX-38", Section = "Vận chuyển & Logistics", Title = "Tổng số ngày trễ hẹn giao hàng (Late Days) theo Đơn vị vận chuyển", BusinessPurpose = "Đo cam kết giao hàng đúng hẹn (SLA).", OperationType = "SLA Performance", SourceFactTable = "Fact_Shipping", OrderNumber = 38, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(38) },
                new() { Id = "MDX-39", Section = "Vận chuyển & Logistics", Title = "Chi phí vận chuyển phân bổ theo Khu vực giao nhận (Region)", BusinessPurpose = "Phân tích chi phí logistics theo vùng miền.", OperationType = "Geographic Aggregation", SourceFactTable = "Fact_Shipping", OrderNumber = 39, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(39) },
                new() { Id = "MDX-40", Section = "Vận chuyển & Logistics", Title = "Tỷ lệ Chi phí vận chuyển trên Doanh thu (Logistics Cost Ratio)", BusinessPurpose = "Kiểm soát để cước vận chuyển không bào mòn lợi nhuận.", OperationType = "Calculated Member", SourceFactTable = "Fact_Shipping", OrderNumber = 40, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(40) },
                new() { Id = "MDX-41", Section = "Xem tổng hợp, chi tiết & lọc", Title = "Gộp doanh thu từ Thành phố lên Khu vực", BusinessPurpose = "Giám đốc: Mỗi miền đóng góp bao nhiêu doanh thu và lợi nhuận?", OperationType = "Roll-up", SourceFactTable = "Fact_Sales", OrderNumber = 41, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(41) },
                new() { Id = "MDX-42", Section = "Xem tổng hợp, chi tiết & lọc", Title = "Gộp doanh thu từ Sản phẩm lên Danh mục, kèm tỷ suất lợi nhuận", BusinessPurpose = "Giám đốc, Trưởng phòng Kinh doanh: Ngành hàng nào vừa lớn về doanh thu vừa có biên lợi nhuận tốt?", OperationType = "Roll-up", SourceFactTable = "Fact_Sales", OrderNumber = 42, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(42) },
                new() { Id = "MDX-43", Section = "Xem tổng hợp, chi tiết & lọc", Title = "Khoan sâu Quý 4/2025 xuống từng Tháng", BusinessPurpose = "Giám đốc: Quý 4/2025 tăng mạnh nhất năm, tháng nào đóng góp nhiều nhất?", OperationType = "Drill-down", SourceFactTable = "Fact_Sales", OrderNumber = 43, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(43) },
                new() { Id = "MDX-44", Section = "Xem tổng hợp, chi tiết & lọc", Title = "Doanh thu sản phẩm tại riêng \"Miền Nam\" (mọi năm)", BusinessPurpose = "Trưởng phòng Kinh doanh: Ở thị trường lớn nhất, nhóm hàng nào bán chạy?", OperationType = "Slice", SourceFactTable = "Fact_Sales", OrderNumber = 44, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(44) },
                new() { Id = "MDX-45", Section = "Xem tổng hợp, chi tiết & lọc", Title = "Doanh thu theo năm của riêng kênh \"Website Khang Nghị\"", BusinessPurpose = "Trưởng phòng Kinh doanh: Kênh bán hàng trực tuyến tăng trưởng thế nào qua các năm?", OperationType = "Slice", SourceFactTable = "Fact_Sales", OrderNumber = 45, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(45) },
                new() { Id = "MDX-46", Section = "Xem tổng hợp, chi tiết & lọc", Title = "Doanh thu sản phẩm theo năm 2024–2025 × Miền Bắc, Miền Trung × kênh Đại lý, Cửa hàng", BusinessPurpose = "Trưởng phòng Kinh doanh: Trong 2 năm gần nhất, kênh trực tiếp (Đại lý, Cửa hàng) ở thị trường ngoài Miền Nam bán nhóm hàng nào?", OperationType = "Dice", SourceFactTable = "Fact_Sales", OrderNumber = 46, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(46) },
                new() { Id = "MDX-47", Section = "Xem tổng hợp, chi tiết & lọc", Title = "Giá trị nhập hàng theo Nhà cung cấp, năm 2025, tại \"Kho TP. Hồ Chí Minh\"", BusinessPurpose = "Trưởng phòng Mua hàng: Năm 2025, kho chính nhập hàng chủ yếu từ nhà cung cấp nào?", OperationType = "Dice", SourceFactTable = "Fact_Purchase", OrderNumber = 47, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(47) },
                new() { Id = "MDX-48", Section = "Xem tổng hợp, chi tiết & lọc", Title = "Ma trận Năm × Khu vực", BusinessPurpose = "Giám đốc: Doanh thu từng miền thay đổi thế nào qua các năm? (Năm trên dòng, Miền trên cột. Đổi trục để xoay góc nhìn.)", OperationType = "Pivot", SourceFactTable = "Fact_Sales", OrderNumber = 48, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(48) },
                new() { Id = "MDX-49", Section = "Xem tổng hợp, chi tiết & lọc", Title = "Tăng trưởng doanh thu từng quý 2025 so với cùng kỳ 2024", BusinessPurpose = "Giám đốc: Quý nào năm 2025 tăng trưởng mạnh nhất so với cùng kỳ năm trước?", OperationType = "Drill-down", SourceFactTable = "Fact_Sales", OrderNumber = 49, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(49) },
                new() { Id = "MDX-50", Section = "Xem tổng hợp, chi tiết & lọc", Title = "Tồn kho cuối kỳ của từng kho, khoan sâu theo Danh mục", BusinessPurpose = "Quản lý kho: Tại kỳ kiểm kê gần nhất, mỗi kho đang giữ bao nhiêu hàng của từng danh mục?", OperationType = "Drill-down", SourceFactTable = "Fact_Inventory", OrderNumber = 50, VerificationStatus = "DESIGN_TEMPLATE", MdxQuery = GetQuestionMdx(50) },
            };
        }
    }
}
