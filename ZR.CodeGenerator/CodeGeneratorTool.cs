﻿using Infrastructure;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ZR.CodeGenerator.CodeGenerator;
using ZR.CodeGenerator.Model;
using ZR.Model.System.Generate;

namespace ZR.CodeGenerator
{
    /// <summary>
    /// 代码生成器。
    /// <remarks>
    /// 根据指定的实体域名空间生成Repositories和Services层的基础代码文件。
    /// </remarks>
    /// </summary>
    public class CodeGeneratorTool
    {
        /// <summary>
        /// 代码生成器配置
        /// </summary>
        private static CodeGenerateOption _option = new CodeGenerateOption();

        /// <summary>
        /// 代码生成器入口方法
        /// </summary>
        /// <param name="dbTableInfo"></param>
        /// <param name="dto"></param>
        public static List<string> Generate(GenTable dbTableInfo, GenerateDto dto)
        {
            _option.BaseNamespace = dbTableInfo.BaseNameSpace;
            _option.DtosNamespace = _option.BaseNamespace + "Model";
            _option.ModelsNamespace = _option.BaseNamespace + "Model";
            _option.RepositoriesNamespace = _option.BaseNamespace + "Repository";
            _option.IRepositoriesNamespace = _option.BaseNamespace + "Repository";
            _option.IServicsNamespace = _option.BaseNamespace + "Service";
            _option.ServicesNamespace = _option.BaseNamespace + "Service";
            _option.ApiControllerNamespace = _option.BaseNamespace + "Admin.WebApi";

            List<string> list = GenerateSingle(dbTableInfo?.Columns, dbTableInfo, dto);

            return list;
        }

        /// <summary>
        /// 单表生成代码
        /// </summary>
        /// <param name="listField">表字段集合</param>
        /// <param name="tableInfo">表信息</param>
        /// <param name="dto"></param>
        public static List<string> GenerateSingle(List<GenTableColumn> listField, GenTable tableInfo, GenerateDto dto)
        {
            string PKName = "id";
            string PKType = "int";
            List<string> genPathList = new();
            ReplaceDto replaceDto = new();
            replaceDto.ModelTypeName = tableInfo.ClassName;//表名对应C# 实体类名
            replaceDto.TableName = tableInfo.TableName;//表名
            replaceDto.TableDesc = tableInfo.TableComment;//表说明描述
            replaceDto.Permission = tableInfo.ClassName.ToLower();//权限
            replaceDto.ViewsFileName = FirstLowerCase(replaceDto.ModelTypeName);
            //循环表字段信息
            foreach (GenTableColumn dbFieldInfo in listField)
            {
                string columnName = dbFieldInfo.ColumnName;

                if (dbFieldInfo.IsInsert || dbFieldInfo.IsEdit)
                {
                    replaceDto.VueViewFormResetHtml += $"        {columnName}: undefined,\r\n";
                }
                if (dbFieldInfo.IsPk || dbFieldInfo.IsIncrement)
                {
                    PKName = dbFieldInfo.CsharpField;
                    PKType = dbFieldInfo.CsharpType;
                }
                //编辑字段
                if (dbFieldInfo.IsEdit)
                {
                    replaceDto.UpdateColumn += $"{dbFieldInfo.CsharpField} = model.{dbFieldInfo.CsharpField}, ";
                }
                //新增字段
                if (dbFieldInfo.IsInsert)
                {
                    replaceDto.InsertColumn += $"it.{dbFieldInfo.CsharpField}, ";
                }
                //TODO 查询
                //if (dbFieldInfo.IsQuery)
                //{
                //    replaceDto.Querycondition += $"predicate = predicate.And(m => m.{dbFieldInfo.CsharpField}.Contains(parm.Name));";
                //}
                if ((dbFieldInfo.HtmlType == GenConstants.HTML_SELECT || dbFieldInfo.HtmlType == GenConstants.HTML_RADIO) && !string.IsNullOrEmpty(dbFieldInfo.DictType))
                {
                    replaceDto.VueDataContent += $"      // {dbFieldInfo.ColumnComment}选项列表\n";
                    replaceDto.VueDataContent += $"      {FirstLowerCase(dbFieldInfo.CsharpField)}Options: [],";

                    replaceDto.MountedMethod += $"   this.getDicts(\"{dbFieldInfo.DictType}\").then((response) => {{\n";
                    replaceDto.MountedMethod += $"      this.{FirstLowerCase(dbFieldInfo.CsharpField)}Options = response.data;\n";
                    replaceDto.MountedMethod += "    })\n";
                }

                replaceDto.QueryProperty += CodeGenerateTemplate.GetQueryDtoProperty(dbFieldInfo);
                replaceDto.ModelProperty += CodeGenerateTemplate.GetModelTemplate(dbFieldInfo);
                replaceDto.VueViewFormHtml += CodeGenerateTemplate.GetVueViewFormContent(dbFieldInfo);
                replaceDto.VueJsMethod += CodeGenerateTemplate.GetVueJsMethod(dbFieldInfo);
                replaceDto.VueViewListHtml += CodeGenerateTemplate.GetTableColumn(dbFieldInfo);
                replaceDto.VueViewEditFormRuleContent += CodeGenerateTemplate.GetFormRules(dbFieldInfo);
                replaceDto.InputDtoProperty += CodeGenerateTemplate.GetDtoProperty(dbFieldInfo);
                replaceDto.VueQueryFormHtml += CodeGenerateTemplate.GetQueryFormHtml(dbFieldInfo);
            }
            replaceDto.PKName = PKName;
            replaceDto.PKType = PKType;

            if (dto.genFiles.Contains(1))
            {
                Tuple<string, string> tuple = GenerateModels(replaceDto, dto);
                genPathList.Add(tuple.Item1);
                WriteAndSave(tuple.Item1, tuple.Item2);
            }
            if (dto.genFiles.Contains(2))
            {
                Tuple<string, string> tuple = GenerateInputDto(replaceDto, dto);
                genPathList.Add(tuple.Item1);
                WriteAndSave(tuple.Item1, tuple.Item2);
            }
            if (dto.genFiles.Contains(3))
            {
                Tuple<string, string> tuple = GenerateRepository(replaceDto, dto);
                genPathList.Add(tuple.Item1);
                WriteAndSave(tuple.Item1, tuple.Item2);
            }
            if (dto.genFiles.Contains(4))
            {
                Tuple<string, string> tuple = GenerateIService(replaceDto, dto);
                Tuple<string, string> tuple_1 = GenerateService(replaceDto, dto);
                genPathList.Add(tuple.Item1);
                genPathList.Add(tuple_1.Item1);
                WriteAndSave(tuple.Item1, tuple.Item2);
                WriteAndSave(tuple_1.Item1, tuple_1.Item2);
            }
            if (dto.genFiles.Contains(5))
            {
                Tuple<string, string> tuple = GenerateControllers(replaceDto, dto);
                genPathList.Add(tuple.Item1);
                WriteAndSave(tuple.Item1, tuple.Item2);
            }
            if (dto.genFiles.Contains(6))
            {
                Tuple<string, string> tuple = GenerateVueViews(replaceDto, dto);
                Tuple<string, string> tuple_1 = GenerateVueJs(replaceDto, dto);
                genPathList.Add(tuple.Item1);
                genPathList.Add(tuple_1.Item1);
                WriteAndSave(tuple.Item1, tuple.Item2);
                WriteAndSave(tuple_1.Item1, tuple_1.Item2);
            }
            if (dto.genFiles.Contains(7))
            {
                Tuple<string, string> tuple = GenerateSql(replaceDto, dto);
                WriteAndSave(tuple.Item1, tuple.Item2);
            }
            return genPathList;
            //GenerateIRepository(modelTypeName, modelTypeDesc, keyTypeName, ifExsitedCovered);
            //GenerateOutputDto(modelTypeName, modelTypeDesc, outputDtocontent, ifExsitedCovered);
        }


        #region 生成Model

        /// <summary>
        /// 生成Models文件
        /// </summary>
        /// <param name="generateDto"></param>
        /// <param name="replaceDto">替换实体</param>
        private static Tuple<string, string> GenerateModels(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            //../ZR.Model
            var servicesPath = Path.Combine(generateDto.GenCodePath, _option.ModelsNamespace, "Models");
            Console.WriteLine("创建文件夹" + servicesPath);
            CreateDirectory(servicesPath);
            // ../ZR.Model/Models/User.cs
            var fullPath = Path.Combine(servicesPath, replaceDto.ModelTypeName + ".cs");

            if (File.Exists(fullPath) && !generateDto.coverd)
                return Tuple.Create(fullPath, "");

            var content = ReadTemplate("ModelTemplate.txt")
                .Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{TableNameDesc}", replaceDto.TableDesc)
                .Replace("{KeyTypeName}", replaceDto.PKName)
                .Replace("{PropertyName}", replaceDto.ModelProperty)
                .Replace("{TableName}", replaceDto.TableName);

            return Tuple.Create(fullPath, content);
        }

        /// <summary>
        /// 生成InputDto文件
        /// </summary>
        /// <param name="generateDto"></param>
        /// <param name="replaceDto">替换实体</param>
        private static Tuple<string, string> GenerateInputDto(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var servicesPath = Path.Combine(generateDto.GenCodePath, _option.ModelsNamespace, "Dto");
            CreateDirectory(servicesPath);
            // ../ZR.Model/Dto/User.cs
            var fullPath = Path.Combine(servicesPath, $"{replaceDto.ModelTypeName}Dto.cs");

            if (File.Exists(fullPath) && !generateDto.coverd)
                return Tuple.Create(fullPath, "");

            var content = ReadTemplate("InputDtoTemplate.txt")
                .Replace("{DtosNamespace}", _option.DtosNamespace)
                .Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{TableNameDesc}", replaceDto.TableDesc)
                .Replace("{PropertyName}", replaceDto.InputDtoProperty)
                .Replace("{QueryProperty}", replaceDto.QueryProperty)
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName);

            return Tuple.Create(fullPath, content);
        }
        #endregion

        #region 生成Repository

        /// <summary>
        /// 生成Repository层代码文件
        /// </summary>
        /// <param name="generateDto"></param>
        /// <param name="replaceDto">替换实体</param>
        private static Tuple<string, string> GenerateRepository(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var repositoryPath = Path.Combine(generateDto.GenCodePath, _option.RepositoriesNamespace, "Repositories");
            CreateDirectory(repositoryPath);

            var fullPath = Path.Combine(repositoryPath, $"{replaceDto.ModelTypeName}Repository.cs");

            if (File.Exists(fullPath) && !generateDto.coverd)
                return Tuple.Create(fullPath, "");

            var content = ReadTemplate("RepositoryTemplate.txt")
                .Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{RepositoriesNamespace}", _option.RepositoriesNamespace)
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{TableNameDesc}", replaceDto.TableDesc)
                .Replace("{TableName}", replaceDto.TableName);

            return Tuple.Create(fullPath, content);
        }

        #endregion

        #region 生成Service
        /// <summary>
        /// 生成IService文件
        /// </summary>
        /// <param name="generateDto"></param>
        /// <param name="replaceDto">替换实体</param>
        private static Tuple<string, string> GenerateIService(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var iServicesPath = Path.Combine(generateDto.GenCodePath, _option.IServicsNamespace, "Business", "IBusService");
            CreateDirectory(iServicesPath);

            var fullPath = Path.Combine(iServicesPath, $"I{replaceDto.ModelTypeName}Service.cs");
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !generateDto.coverd)
                return Tuple.Create(fullPath, "");
            var content = ReadTemplate("IServiceTemplate.txt")
                .Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{TableNameDesc}", replaceDto.TableDesc)
                .Replace("{DtosNamespace}", _option.DtosNamespace)
                .Replace("{IServicsNamespace}", _option.IServicsNamespace)
                .Replace("{RepositoriesNamespace}", _option.RepositoriesNamespace)
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName);

            return Tuple.Create(fullPath, content);
        }

        /// <summary>
        /// 生成Service文件
        /// </summary>
        private static Tuple<string, string> GenerateService(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var servicesPath = Path.Combine(generateDto.GenCodePath, _option.ServicesNamespace, "Business");
            CreateDirectory(servicesPath);
            var fullPath = Path.Combine(servicesPath, $"{replaceDto.ModelTypeName}Service.cs");
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !generateDto.coverd)
                return Tuple.Create(fullPath, "");

            var content = ReadTemplate("ServiceTemplate.txt")
                .Replace("{IRepositoriesNamespace}", _option.IRepositoriesNamespace)
                .Replace("{DtosNamespace}", _option.DtosNamespace)
                .Replace("{IServicsNamespace}", _option.IServicsNamespace)
                .Replace("{TableNameDesc}", replaceDto.TableDesc)
                .Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{ServicesNamespace}", _option.ServicesNamespace)
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName);

            return Tuple.Create(fullPath, content);
        }

        #endregion

        #region 生成Controller
        /// <summary>
        /// 生成控制器ApiControllers文件
        /// </summary>
        private static Tuple<string, string> GenerateControllers(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var servicesPath = Path.Combine(generateDto.GenCodePath, _option.ApiControllerNamespace, "Controllers", "business");
            CreateDirectory(servicesPath);

            var fullPath = Path.Combine(servicesPath, $"{replaceDto.ModelTypeName}Controller.cs");
            if (File.Exists(fullPath) && !generateDto.coverd)
                return Tuple.Create(fullPath, "");

            var content = ReadTemplate("ControllersTemplate.txt")
                .Replace("{ApiControllerNamespace}", _option.ApiControllerNamespace)
                .Replace("{ServicesNamespace}", _option.ServicesNamespace)
                .Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{TableDesc}", replaceDto.TableDesc)
                .Replace("{ModelName}", replaceDto.ModelTypeName)
                .Replace("{Permission}", replaceDto.Permission)
                .Replace("{PrimaryKey}", replaceDto.PKName)
                .Replace("{UpdateColumn}", replaceDto.UpdateColumn)
                .Replace("{InsertColumn}", replaceDto.InsertColumn)
                .Replace("{PKCsharpType}", replaceDto.PKType);

            return Tuple.Create(fullPath, content);
        }
        #endregion

        #region 生成Vue页面
        /// <summary>
        /// 生成Vue页面
        private static Tuple<string, string> GenerateVueViews(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var parentPath = Path.Combine(generateDto.GenCodePath, "ZR.Vue", "src");
            var servicesPath = Path.Combine(parentPath, "views", replaceDto.ViewsFileName);
            CreateDirectory(servicesPath);

            var fullPath = Path.Combine(servicesPath, $"{replaceDto.ViewsFileName}.vue");

            if (File.Exists(fullPath) && !generateDto.coverd)
                return Tuple.Create(fullPath, "");

            var content = ReadTemplate("VueTemplate.txt")
                .Replace("{fileClassName}", replaceDto.ViewsFileName)
                .Replace("{VueViewListContent}", replaceDto.VueViewListHtml)//查询 table列
                .Replace("{VueViewFormContent}", replaceDto.VueViewFormHtml)//添加、修改表单
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{Permission}", replaceDto.Permission)
                .Replace("{VueViewFormResetHtml}", replaceDto.VueViewFormResetHtml)
                .Replace("{vueJsMethod}", replaceDto.VueJsMethod)
                .Replace("{vueQueryFormHtml}", replaceDto.VueQueryFormHtml)
                .Replace("{VueDataContent}", replaceDto.VueDataContent)
                .Replace("{primaryKey}", FirstLowerCase(replaceDto.PKName))
                .Replace("{MountedMethod}", replaceDto.MountedMethod)
                .Replace("{VueViewEditFormRuleContent}", replaceDto.VueViewEditFormRuleContent);//添加、修改表单验证规则

            return Tuple.Create(fullPath, content);
        }
        public static Tuple<string, string> GenerateVueJs(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            //api js
            var parentPath = Path.Combine(generateDto.GenCodePath, "ZR.Vue", "src");
            string servicesPath = Path.Combine(parentPath, "api");
            CreateDirectory(servicesPath);

            string fullPath = Path.Combine(servicesPath, replaceDto.ViewsFileName + ".js");

            if (File.Exists(fullPath) && !generateDto.coverd)
                return Tuple.Create(fullPath, "");
            
            var content = ReadTemplate("VueJsTemplate.txt")
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{ModelTypeDesc}", replaceDto.TableDesc);

            return Tuple.Create(fullPath, content);
        }

        #endregion

        #region 生成SQL

        public static Tuple<string, string> GenerateSql(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            string fullPath = Path.Combine(generateDto.GenCodePath, replaceDto.ViewsFileName + ".sql");

            if (File.Exists(fullPath) && !generateDto.coverd)
                return Tuple.Create(fullPath, "");
            var content = ReadTemplate("SqlTemplate.txt")
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{Permission}", replaceDto.Permission)
                .Replace("{ModelTypeDesc}", replaceDto.TableDesc);

            return Tuple.Create(fullPath, content);
        }

        #endregion

        #region 帮助方法

        /// <summary>
        /// 如果有前缀替换将前缀替换成空，替换下划线"_"为空再将首字母大写
        /// 表名转换成C#类名
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public static string GetClassName(string tableName)
        {
            bool autoRemovePre = ConfigUtils.Instance.GetAppConfig(GenConstants.Gen_autoPre, false);
            string tablePrefix = ConfigUtils.Instance.GetAppConfig<string>(GenConstants.Gen_tablePrefix);

            if (!string.IsNullOrEmpty(tablePrefix) && autoRemovePre)
            {
                string[] searcList = tablePrefix.Split(",", StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < searcList.Length; i++)
                {
                    if (!string.IsNullOrEmpty(searcList[i].ToString()))
                    {
                        tableName = tableName.Replace(searcList[i], "");
                    }
                }
            }
            return tableName.Substring(0, 1).ToUpper() + tableName[1..].Replace("_", "");
        }

        /// <summary>
        /// 首字母转小写，输出前端
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string FirstLowerCase(string str)
        {
            return string.IsNullOrEmpty(str) ? str : str.Substring(0, 1).ToLower() + str[1..];
        }

        /// <summary>
        /// 获取前端标签名
        /// </summary>
        /// <param name="columnDescription"></param>
        /// <param name="columnName"></param>
        /// <returns></returns>
        public static string GetLabelName(string columnDescription, string columnName)
        {
            return string.IsNullOrEmpty(columnDescription) ? columnName : columnDescription;
        }
        /// <summary>
        /// 从代码模板中读取内容
        /// </summary>
        /// <param name="templateName">模板名称，应包括文件扩展名称。比如：template.txt</param>
        /// <returns></returns>
        private static string ReadTemplate(string templateName)
        {
            string path = Environment.CurrentDirectory;
            string fullName = $"{path}/wwwroot/CodeGenTemplate/{templateName}";

            Console.WriteLine("开始读取模板=" + fullName);
            string temp = fullName;
            string str = "";
            if (!File.Exists(temp))
            {
                return str;
            }
            StreamReader sr = null;
            try
            {
                sr = new StreamReader(temp);
                str = sr.ReadToEnd(); // 读取文件
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取模板出错了{ex.Message}");
            }
            sr?.Close();
            sr?.Dispose();
            return str;
        }

        /// <summary>
        /// 写文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="content"></param>
        private static void WriteAndSave(string fileName, string content)
        {
            fileName = fileName.Replace("\\", "/").Replace("//", "/");
            Console.WriteLine("写入文件：" + fileName);
            try
            {
                //实例化一个文件流--->与写入文件相关联
                using var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
                //实例化一个StreamWriter-->与fs相关联
                using var sw = new StreamWriter(fs);
                //开始写入
                sw.Write(content);
                //清空缓冲区
                sw.Flush();
                //关闭流
                sw.Close();
                fs.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("写入文件出错了:" + ex.Message);
            }
        }

        /// <summary>
        /// 创建文件夹
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool CreateDirectory(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                path = path.Replace("\\", "/").Replace("//", "/");
            }
            try
            {
                if (!Directory.Exists(path))
                {
                    DirectoryInfo info = Directory.CreateDirectory(path);
                    Console.WriteLine("不存在创建文件夹" + info);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建文件夹出错了,{ex.Message}");
                return false;
            }
            return true;
        }

        #endregion

        /// <summary>
        /// 初始化列属性字段数据
        /// </summary>
        /// <param name="genTable"></param>
        /// <param name="dbColumnInfos"></param>
        public static List<GenTableColumn> InitGenTableColumn(GenTable genTable, List<DbColumnInfo> dbColumnInfos)
        {
            List<GenTableColumn> genTableColumns = new();
            foreach (var column in dbColumnInfos)
            {
                GenTableColumn genTableColumn = new()
                {
                    ColumnName = FirstLowerCase(column.DbColumnName),
                    ColumnComment = column.ColumnDescription,
                    IsPk = column.IsPrimarykey,
                    ColumnType = column.DataType,
                    TableId = genTable.TableId,
                    TableName = genTable.TableName,
                    CsharpType = TableMappingHelper.GetCSharpDatatype(column.DataType),
                    CsharpField = column.DbColumnName.Substring(0, 1).ToUpper() + column.DbColumnName[1..],
                    IsRequired = !column.IsNullable,
                    IsIncrement = column.IsIdentity,
                    Create_by = genTable.Create_by,
                    Create_time = DateTime.Now,
                    IsInsert = !column.IsIdentity,//非自增字段都需要插入
                    IsQuery = false,
                    HtmlType = GenConstants.HTML_INPUT
                };

                if (GenConstants.imageFiled.Any(f => column.DbColumnName.ToLower().Contains(f.ToLower())))
                {
                    genTableColumn.HtmlType = GenConstants.HTML_IMAGE_UPLOAD;
                }
                else if (GenConstants.COLUMNTYPE_TIME.Any(f => genTableColumn.CsharpType.ToLower().Contains(f.ToLower())))
                {
                    genTableColumn.HtmlType = GenConstants.HTML_DATETIME;
                }
                else if (GenConstants.radioFiled.Any(f => column.DbColumnName.EndsWith(f, StringComparison.OrdinalIgnoreCase)) ||
                    GenConstants.radioFiled.Any(f => column.DbColumnName.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
                {
                    genTableColumn.HtmlType = GenConstants.HTML_RADIO;
                }
                else if (GenConstants.selectFiled.Any(f => column.DbColumnName == f) ||
                    GenConstants.selectFiled.Any(f => column.DbColumnName.EndsWith(f, StringComparison.OrdinalIgnoreCase)))
                {
                    genTableColumn.HtmlType = GenConstants.HTML_SELECT;
                }
                else if (column.Length > 500)
                {
                    genTableColumn.HtmlType = GenConstants.HTML_TEXTAREA;
                }
                //编辑字段
                if (GenConstants.COLUMNNAME_NOT_EDIT.Any(f => column.DbColumnName.Contains(f)) && !column.IsIdentity && !column.IsPrimarykey)
                {
                    genTableColumn.IsEdit = true;
                }
                //列表字段
                if (!GenConstants.COLUMNNAME_NOT_LIST.Any(f => column.DbColumnName.Contains(f) && !column.IsPrimarykey))
                {
                    genTableColumn.IsList = true;
                }

                genTableColumns.Add(genTableColumn);
            }
            return genTableColumns;
        }

        /// <summary>
        /// 压缩代码
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public static string ZipGenCode(GenerateDto dto)
        {
            try
            {
                //生成压缩包
                string zipReturnFileName = "ZR." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".zip";

                CreateDirectory(dto.GenCodePath);
                string zipFileName = Path.Combine(dto.ZipPath, zipReturnFileName);
                if (File.Exists(zipFileName))
                {
                    File.Delete(zipFileName);
                }

                ZipFile.CreateFromDirectory(dto.GenCodePath, zipFileName);
                FileHelper.DeleteDirectory(dto.GenCodePath);
                dto.ZipFileName = zipReturnFileName;
                return zipReturnFileName;
            }
            catch (Exception ex)
            {
                Console.WriteLine("压缩文件出错。" + ex.Message);
                return "";
            }
        }

    }
}
