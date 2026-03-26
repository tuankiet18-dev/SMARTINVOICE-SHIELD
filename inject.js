const fs = require('fs');

const pathSettingsService = '../SmartInvoice.Frontend/src/services/settingsService.ts';
let settingsSvc = fs.readFileSync(pathSettingsService, 'utf8');

settingsSvc = settingsSvc.replace('autoApproveThreshold: number;\n}', 'autoApproveThreshold: number;\n  requireTwoStepApproval: boolean;\n  twoStepApprovalThreshold: number;\n  hasAdvancedWorkflow: boolean;\n}');
settingsSvc = settingsSvc.replace('autoApproveThreshold: number;\n}', 'autoApproveThreshold: number;\n  requireTwoStepApproval: boolean;\n  twoStepApprovalThreshold: number;\n}');

fs.writeFileSync(pathSettingsService, settingsSvc, 'utf8');


const pathSettings = '../SmartInvoice.Frontend/src/pages/Settings.tsx';
let settingsTxt = fs.readFileSync(pathSettings, 'utf8');

// replace useEffect mapping
settingsTxt = settingsTxt.replace(
  'autoApproveThreshold: company.autoApproveThreshold,', 
  `autoApproveThreshold: company.autoApproveThreshold,
        requireTwoStepApproval: company.requireTwoStepApproval,
        twoStepApprovalThreshold: company.twoStepApprovalThreshold,`
);

// add to onCompanyFinish
settingsTxt = settingsTxt.replace(
  'autoApproveThreshold: allValues.autoApproveThreshold ?? company?.autoApproveThreshold ?? 0,',
  `autoApproveThreshold: allValues.autoApproveThreshold ?? company?.autoApproveThreshold ?? 0,
      requireTwoStepApproval: allValues.requireTwoStepApproval ?? company?.requireTwoStepApproval ?? false,
      twoStepApprovalThreshold: allValues.twoStepApprovalThreshold ?? company?.twoStepApprovalThreshold ?? 20000000,`
);

// Try to find the end of the isAutoApproveEnabled section and add the Two-Step logic 
const autoApproveEnd = settingsTxt.indexOf('</Form.Item>', settingsTxt.indexOf('name="autoApproveThreshold"')) + 12;

const twoStepUI = `
                  </div>
                  
                  {company?.hasAdvancedWorkflow && (
                    <>
                      <Divider style={{ margin: "24px 0" }} />
                      <div style={{ marginBottom: 24 }}>
                        <div
                          style={{
                            display: "flex",
                            justifyContent: "space-between",
                            alignItems: "center",
                            marginBottom: 16,
                          }}
                        >
                          <div>
                            <Text strong style={{ fontSize: 16 }}>
                              Quy trình duyệt 2 lớp
                            </Text>
                            <br />
                            <Text type="secondary">
                              Yêu cầu duyệt qua 2 bước bảo mật cho các hóa đơn có giá trị lớn.
                            </Text>
                          </div>
                          <Form.Item
                            name="requireTwoStepApproval"
                            valuePropName="checked"
                            style={{ margin: 0 }}
                          >
                            <Switch checkedChildren="Bật" unCheckedChildren="Tắt" />
                          </Form.Item>
                        </div>
                        <Form.Item
                          noStyle
                          shouldUpdate={(prevValues, currentValues) =>
                            prevValues.requireTwoStepApproval !== currentValues.requireTwoStepApproval
                          }
                        >
                          {({ getFieldValue }) => {
                            const isEnabled = getFieldValue("requireTwoStepApproval");
                            return (
                              <div
                                style={{
                                  opacity: isEnabled ? 1 : 0.5,
                                  pointerEvents: isEnabled ? "auto" : "none",
                                  padding: "16px",
                                  background: "#F8FAFC",
                                  borderRadius: 8,
                                }}
                              >
                                <Form.Item
                                  label="Ngưỡng duyệt 2 lớp (VND)"
                                  name="twoStepApprovalThreshold"
                                  rules={[
                                    {
                                      required: isEnabled,
                                      message: "Vui lòng nhập định mức",
                                    },
                                  ]}
                                  tooltip="Hóa đơn lớn hơn hoặc bằng mức này sẽ cần 2 người phê duyệt."
                                >
                                  <InputNumber
                                    style={{ width: "100%", maxWidth: 300 }}
                                    formatter={(value) =>
                                      \`\${value}\`.replace(/\\B(?=(\\d{3})+(?!\\d))/g, ",")
                                    }
                                    parser={(value) =>
                                      value?.replace(/\\$\\s?|(,*)/g, "") as unknown as number
                                    }
                                    addonAfter="VND"
                                    min={0}
                                    step={100000}
                                  />
                                </Form.Item>
                                <Text type="secondary" style={{ fontSize: 13 }}>
                                  * Chức năng này yêu cầu người dùng duyệt Cấp 1 để chuyển trạng thái, sau đó người duyệt Cấp 2 mới có quyền chốt hóa đơn.
                                </Text>
                              </div>
                            );
                          }}
                        </Form.Item>
                      </div>
                    </>
                  )}
`;

settingsTxt = settingsTxt.slice(0, autoApproveEnd) + "\n" + twoStepUI + settingsTxt.slice(autoApproveEnd);
fs.writeFileSync(pathSettings, settingsTxt, 'utf8');

console.log("Success");
