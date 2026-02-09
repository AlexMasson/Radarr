import PropTypes from 'prop-types';
import React from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

function DownloadClientOptions(props) {
  const {
    advancedSettings,
    isFetching,
    error,
    settings,
    hasSettings,
    onInputChange
  } = props;

  return (
    <div>
      {
        isFetching &&
          <LoadingIndicator />
      }

      {
        !isFetching && error &&
          <Alert kind={kinds.DANGER}>
            {translate('DownloadClientOptionsLoadError')}
          </Alert>
      }

      {
        hasSettings && !isFetching && !error && advancedSettings &&
          <div>
            <FieldSet legend={translate('CompletedDownloadHandling')}>

              <Form>
                <FormGroup
                  advancedSettings={advancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel>{translate('Enable')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="enableCompletedDownloadHandling"
                    helpText={translate('EnableCompletedDownloadHandlingHelpText')}
                    onChange={onInputChange}
                    {...settings.enableCompletedDownloadHandling}
                  />
                </FormGroup>

                <FormGroup
                  advancedSettings={advancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel>{translate('CheckForFinishedDownloadsInterval')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.NUMBER}
                    name="checkForFinishedDownloadInterval"
                    min={1}
                    max={120}
                    unit="minutes"
                    helpText={translate('RefreshMonitoredIntervalHelpText')}
                    onChange={onInputChange}
                    {...settings.checkForFinishedDownloadInterval}
                  />
                </FormGroup>
              </Form>
            </FieldSet>

            <FieldSet
              legend={translate('FailedDownloadHandling')}
            >
              <Form>
                <FormGroup
                  advancedSettings={advancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel>{translate('AutoRedownloadFailed')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="autoRedownloadFailed"
                    helpText={translate('AutoRedownloadFailedHelpText')}
                    onChange={onInputChange}
                    {...settings.autoRedownloadFailed}
                  />
                </FormGroup>

                {
                  settings.autoRedownloadFailed.value ?
                    <FormGroup
                      advancedSettings={advancedSettings}
                      isAdvanced={true}
                      size={sizes.MEDIUM}
                    >
                      <FormLabel>{translate('AutoRedownloadFailedFromInteractiveSearch')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.CHECK}
                        name="autoRedownloadFailedFromInteractiveSearch"
                        helpText={translate('AutoRedownloadFailedFromInteractiveSearchHelpText')}
                        onChange={onInputChange}
                        {...settings.autoRedownloadFailedFromInteractiveSearch}
                      />
                    </FormGroup> :
                    null
                }
              </Form>

              <Alert kind={kinds.INFO}>
                {translate('RemoveDownloadsAlert')}
              </Alert>
            </FieldSet>

            <FieldSet legend={translate('ExternalHooks')}>
              <Alert kind={kinds.INFO}>
                {translate('ExternalHooksHelpText')}
              </Alert>

              <Form>
                <FormGroup
                  advancedSettings={advancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel>{translate('ExternalRejectionHookEnabled')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="externalRejectionHookEnabled"
                    helpText={translate('ExternalRejectionHookEnabledHelpText')}
                    onChange={onInputChange}
                    {...settings.externalRejectionHookEnabled}
                  />
                </FormGroup>

                {
                  settings.externalRejectionHookEnabled.value ?
                    <FormGroup
                      advancedSettings={advancedSettings}
                      isAdvanced={true}
                    >
                      <FormLabel>{translate('ExternalRejectionHookUrl')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.TEXT}
                        name="externalRejectionHookUrl"
                        helpText={translate('ExternalRejectionHookUrlHelpText')}
                        onChange={onInputChange}
                        {...settings.externalRejectionHookUrl}
                      />
                    </FormGroup> :
                    null
                }

                {
                  settings.externalRejectionHookEnabled.value ?
                    <FormGroup
                      advancedSettings={advancedSettings}
                      isAdvanced={true}
                    >
                      <FormLabel>{translate('ExternalRejectionHookTimeout')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.NUMBER}
                        name="externalRejectionHookTimeout"
                        min={1}
                        max={300}
                        unit="seconds"
                        helpText={translate('ExternalRejectionHookTimeoutHelpText')}
                        onChange={onInputChange}
                        {...settings.externalRejectionHookTimeout}
                      />
                    </FormGroup> :
                    null
                }

                <FormGroup
                  advancedSettings={advancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel>{translate('ExternalPrioritizationHookEnabled')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="externalPrioritizationHookEnabled"
                    helpText={translate('ExternalPrioritizationHookEnabledHelpText')}
                    onChange={onInputChange}
                    {...settings.externalPrioritizationHookEnabled}
                  />
                </FormGroup>

                {
                  settings.externalPrioritizationHookEnabled.value ?
                    <FormGroup
                      advancedSettings={advancedSettings}
                      isAdvanced={true}
                    >
                      <FormLabel>{translate('ExternalPrioritizationHookUrl')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.TEXT}
                        name="externalPrioritizationHookUrl"
                        helpText={translate('ExternalPrioritizationHookUrlHelpText')}
                        onChange={onInputChange}
                        {...settings.externalPrioritizationHookUrl}
                      />
                    </FormGroup> :
                    null
                }

                {
                  settings.externalPrioritizationHookEnabled.value ?
                    <FormGroup
                      advancedSettings={advancedSettings}
                      isAdvanced={true}
                    >
                      <FormLabel>{translate('ExternalPrioritizationHookTimeout')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.NUMBER}
                        name="externalPrioritizationHookTimeout"
                        min={1}
                        max={300}
                        unit="seconds"
                        helpText={translate('ExternalPrioritizationHookTimeoutHelpText')}
                        onChange={onInputChange}
                        {...settings.externalPrioritizationHookTimeout}
                      />
                    </FormGroup> :
                    null
                }

                {
                  (settings.externalRejectionHookEnabled.value || settings.externalPrioritizationHookEnabled.value) ?
                    <FormGroup
                      advancedSettings={advancedSettings}
                      isAdvanced={true}
                    >
                      <FormLabel>{translate('ExternalHooksUsername')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.TEXT}
                        name="externalHooksUsername"
                        helpText={translate('ExternalHooksUsernameHelpText')}
                        onChange={onInputChange}
                        {...settings.externalHooksUsername}
                      />
                    </FormGroup> :
                    null
                }

                {
                  (settings.externalRejectionHookEnabled.value || settings.externalPrioritizationHookEnabled.value) ?
                    <FormGroup
                      advancedSettings={advancedSettings}
                      isAdvanced={true}
                    >
                      <FormLabel>{translate('ExternalHooksPassword')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.PASSWORD}
                        name="externalHooksPassword"
                        helpText={translate('ExternalHooksPasswordHelpText')}
                        onChange={onInputChange}
                        {...settings.externalHooksPassword}
                      />
                    </FormGroup> :
                    null
                }

                {
                  (settings.externalRejectionHookEnabled.value || settings.externalPrioritizationHookEnabled.value) ?
                    <FormGroup
                      advancedSettings={advancedSettings}
                      isAdvanced={true}
                    >
                      <FormLabel>{translate('ExternalHooksSkipTag')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.TEXT}
                        name="externalHooksSkipTag"
                        helpText={translate('ExternalHooksSkipTagHelpText')}
                        onChange={onInputChange}
                        {...settings.externalHooksSkipTag}
                      />
                    </FormGroup> :
                    null
                }
              </Form>
            </FieldSet>
          </div>
      }
    </div>
  );
}

DownloadClientOptions.propTypes = {
  advancedSettings: PropTypes.bool.isRequired,
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  settings: PropTypes.object.isRequired,
  hasSettings: PropTypes.bool.isRequired,
  onInputChange: PropTypes.func.isRequired
};

export default DownloadClientOptions;
